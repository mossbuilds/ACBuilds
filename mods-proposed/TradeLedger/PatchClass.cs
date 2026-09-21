using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace TradeLedger;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static string Describe(Player p, out long coins, Settings cfg)
    {
        coins = 0;
        var parts = new List<string>();
        foreach (var g in p.ItemsInTradeWindow.ToList())
        {
            var wo = p.GetInventoryItem(g) ?? p.GetEquippedItem(g);
            if (wo == null) continue;
            var n = wo.StackSize ?? 1;
            if (wo.WeenieType == WeenieType.Coin) { coins += n; continue; }
            parts.Add(n > 1 ? $"{wo.Name} x{n}" : wo.Name);
        }
        return cfg.LogItems ? (parts.Count == 0 ? "-" : string.Join(", ", parts)) : $"{parts.Count} item(s)";
    }

    // Player.FinalizeTrade(Player target) is private void (ACE.Server.WorldObjects, Player_Trade.cs). It returns early on failed
    // verification and otherwise sets TradeTransferInProgress = true on both players, so the postfix logs only when that flag is set.
    // The prefix snapshots the trade window (the items are removed inside the method). Never blocks, never throws.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "FinalizeTrade", new Type[] { typeof(Player) })]
    public static void PreFinalize(Player __instance, Player target, ref (string, string, long, long, bool)? __state)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || target == null) return;
            var a = Describe(__instance, out var ca, cfg);
            var b = Describe(target, out var cb, cfg);
            __state = (a, b, ca, cb, true);
        }
        catch (Exception e) { ModManager.Log($"[TradeLedger] pre: {e.Message}"); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "FinalizeTrade", new Type[] { typeof(Player) })]
    public static void PostFinalize(Player __instance, Player target, (string, string, long, long, bool)? __state)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || target == null || __state == null) return;
            if (!__instance.TradeTransferInProgress) return; // verification failed, nothing traded
            var (a, b, ca, cb, _) = __state.Value;
            var coins = cfg.LogCoins ? $" | pyreals {ca}/{cb}" : "";
            var lb = __instance.Location?.Landblock ?? 0;
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | {__instance.Name} <-> {target.Name} | {__instance.Name} gave: {a} | {target.Name} gave: {b}{coins} | lb {lb:X4}";
            Write(cfg, line);
        }
        catch (Exception e) { ModManager.Log($"[TradeLedger] post: {e.Message}"); }
    }

    private static void Write(Settings cfg, string line)
    {
        lock (gate)
        {
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

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    [CommandHandler("tradelog", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Shows the last N trades (max 50) or those involving a character.", "[n | character name]")]
    public static void HandleTradeLog(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        string[] lines;
        lock (gate) lines = File.Exists(cfg.LogFile) ? File.ReadAllLines(cfg.LogFile) : Array.Empty<string>();
        var n = 10;
        IEnumerable<string> q = lines;
        if (parameters.Length > 0)
        {
            if (parameters.Length == 1 && int.TryParse(parameters[0], out var v)) n = Math.Clamp(v, 1, 50);
            else
            {
                n = 50;
                var name = string.Join(" ", parameters);
                q = lines.Where(l => l.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
        }
        var list = q.TakeLast(n).ToList();
        if (list.Count == 0) { Say(session, "No trades logged."); return; }
        foreach (var l in list) Say(session, l);
    }
}
