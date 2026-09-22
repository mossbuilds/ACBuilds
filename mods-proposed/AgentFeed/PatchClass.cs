using System.Net.Http;
using System.Text;
using System.Text.Json;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AgentFeed;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    // Used only for the optional outbound webhook (Settings.WebhookUrl). This is the only network call in this mod
    // suite; everything else is file-based. A single static HttpClient is reused across calls per Microsoft guidance.
    private static readonly HttpClient http = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Emit(string eventType, string charName, string extra, object? webhookFields = null)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var now = DateTime.UtcNow;
        var line = $"{now:yyyy-MM-dd HH:mm:ss}Z | {eventType} | {charName}{(string.IsNullOrEmpty(extra) ? "" : " | " + extra)}";
        Write(cfg, line);

        if (!string.IsNullOrWhiteSpace(cfg.WebhookUrl))
            PostWebhook(cfg, eventType, charName, now, webhookFields);
    }

    private static void Write(Settings cfg, string line)
    {
        try
        {
            lock (gate)
            {
                var dir = Path.GetDirectoryName(cfg.LogFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var fi = new FileInfo(cfg.LogFile);
                if (fi.Exists && fi.Length > cfg.MaxKb * 1024L)
                {
                    for (var i = cfg.MaxFiles; i >= 1; i--)
                    {
                        var src = i == 1 ? cfg.LogFile : $"{cfg.LogFile}.{i - 1}";
                        var dst = $"{cfg.LogFile}.{i}";
                        if (!File.Exists(src)) continue;
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }
                File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
            }
        }
        catch (Exception e) { ModManager.Log($"[AgentFeed] write: {e.Message}"); }
    }

    // Fire-and-forget: never awaited by the caller, runs on a threadpool thread, every exception is swallowed here
    // (and logged) so it can never propagate back into ACE or block the game thread.
    private static void PostWebhook(Settings cfg, string eventType, string charName, DateTime utc, object? fields)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var body = new Dictionary<string, object?>
                {
                    ["time"] = utc.ToString("o"),
                    ["event"] = eventType,
                    ["character"] = charName,
                };
                if (fields is Dictionary<string, object?> extra)
                    foreach (var kv in extra) body[kv.Key] = kv.Value;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, cfg.WebhookTimeoutSeconds)));
                using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                using var resp = await http.PostAsync(cfg.WebhookUrl, content, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e) { ModManager.Log($"[AgentFeed] webhook: {e.Message}"); }
        });
    }

    // Player.PlayerEnterWorld is verified in Player_Networking.cs (see LoginGreeter's postfix on the same method).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnterWorld(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || !cfg.LogLogin) return;
            Emit("login", __instance.Name, "");
        }
        catch (Exception e) { ModManager.Log($"[AgentFeed] login: {e.Message}"); }
    }

    // Player.LogOut_Inner(bool) verified in Player_Networking.cs; same signature MinionCleanup patches (its prefix
    // runs on the same method to sweep leftover minions before the character actually leaves).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.LogOut_Inner), new[] { typeof(bool) })]
    public static void PostLogout(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || !cfg.LogLogout) return;
            Emit("logout", __instance.Name, "");
        }
        catch (Exception e) { ModManager.Log($"[AgentFeed] logout: {e.Message}"); }
    }

    // Player.Die(DamageHistoryInfo, DamageHistoryInfo) is protected (Player_Combat.cs); patched by name with
    // explicit ArgumentTypes, same signature MinionCleanup already patches (DamageHistoryInfo, ACE.Server.Entity).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Die", new[] { typeof(DamageHistoryInfo), typeof(DamageHistoryInfo) })]
    public static void PostDie(Player __instance, DamageHistoryInfo topDamager, DamageHistoryInfo lastDamager)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || !cfg.LogDeath) return;
            // DamageHistoryInfo carries the attacker's ObjectGuid; resolve to a name if the attacker is a live
            // world object (a monster wcid/name), never an account or IP. Fall back to "-" if it cannot be resolved.
            var lb = __instance.Location?.Landblock ?? 0;
            string killer = "-";
            try
            {
                var guid = lastDamager?.Guid ?? topDamager?.Guid;
                if (guid != null)
                {
                    var wo = __instance.CurrentLandblock?.GetObject(guid.Value);
                    if (wo != null) killer = wo.Name;
                }
            }
            catch { /* best-effort only */ }
            Emit("death", __instance.Name, $"killed by {killer} | lb {lb:X4}",
                new Dictionary<string, object?> { ["killed_by"] = killer, ["landblock"] = $"{lb:X4}" });
        }
        catch (Exception e) { ModManager.Log($"[AgentFeed] death: {e.Message}"); }
    }

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // "agentfeed" checked against the built-in command list (%TEMP%\cmds.txt) - not a built-in. Read-only: shows the
    // log path and current settings, never toggles anything (Enabled/WebhookUrl are Settings.json-only).
    [CommandHandler("agentfeed", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Shows AgentFeed's log path and current settings (read-only).", "")]
    public static void HandleStatus(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        Say(session, $"AgentFeed: enabled={cfg.Enabled} log={cfg.LogFile} maxKb={cfg.MaxKb} maxFiles={cfg.MaxFiles}");
        Say(session, $"  logging: login={cfg.LogLogin} logout={cfg.LogLogout} death={cfg.LogDeath}");
        Say(session, $"  webhook: {(string.IsNullOrWhiteSpace(cfg.WebhookUrl) ? "off" : "on -> " + cfg.WebhookUrl)}");
    }
}
