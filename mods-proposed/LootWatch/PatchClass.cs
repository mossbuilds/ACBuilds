using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace LootWatch;

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

    // Corpse.Open(Player player) is public override void (ACE.Server.WorldObjects, Corpse.cs). It checks HasPermission(player) first
    // and returns early WITHOUT calling base.Open (Container.Open) when permission is denied - IsOpen/Viewer are only set by base.Open
    // on success. The postfix therefore re-checks __instance.IsOpen && __instance.Viewer == player.Guid.Full to confirm the open
    // actually happened, instead of re-calling HasPermission(player) itself - HasPermission has side effects (it mutates the
    // permitteeOpened set and removes entries from player.LootPermission on a passing call), so calling it a second time from
    // here would double those side effects. This patch never reads or changes the outcome; it only observes it after the fact.
    //
    // Only monster corpses are logged (Corpse.IsMonster, set true in Creature_Death.cs's CreateCorpse - player corpses are out of
    // scope for this idea). "Not in the killer's fellowship" is computed the same way Corpse.HasPermission's own fellowship-share
    // branch does (player.Fellowship == killer's online Fellowship), purely as a read, never by calling HasPermission again.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.Open), new Type[] { typeof(Player) })]
    public static void PostOpen(Corpse __instance, Player player)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || player == null) return;
            if (!__instance.IsMonster) return; // player-corpse ninja-loot is out of scope for this idea
            if (!__instance.IsOpen || __instance.Viewer != player.Guid.Full) return; // Open() returned early, permission was denied

            var killerId = __instance.KillerId;
            var isKiller = killerId != null && player.Guid.Full == killerId.Value;
            if (isKiller) return; // the recorded killer looting their own kill is not a dispute case

            var inKillersFellowship = false;
            if (player.Fellowship != null && killerId != null)
            {
                var killerOnline = PlayerManager.GetOnlinePlayer(killerId.Value);
                if (killerOnline != null && killerOnline.Fellowship == player.Fellowship)
                    inKillersFellowship = true;
            }
            if (inKillersFellowship) return; // shared with the killer's party, not a ninja-loot candidate

            var lb = player.Location?.Landblock ?? 0;
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | opener {player.Name} | corpse {__instance.Name} | killer {killerId?.ToString() ?? "none"} | looted {__instance.IsLooted} | lb {lb:X4}";
            Write(cfg, line);
        }
        catch (Exception e) { ModManager.Log($"[LootWatch] {e.Message}"); }
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

    [CommandHandler("lootlog", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Shows the last N logged loot-audit lines (max 50) or those involving a character.", "[n | character name]")]
    public static void HandleLootLog(Session session, params string[] parameters)
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
        if (list.Count == 0) { Say(session, "No loot-audit lines logged."); return; }
        foreach (var l in list) Say(session, l);
    }
}
