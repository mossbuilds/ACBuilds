using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace IdleKick;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static readonly Dictionary<uint, DateTime> lastActive = new();
    private static readonly Dictionary<uint, DateTime> warnedAt = new();

    // Activity: Player.OnMoveToState(MoveToState) verified in Player_Tick.cs.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnMoveToState), new[] { typeof(ACE.Server.Network.Structure.MoveToState) })]
    public static void PostMove(Player __instance) => Touch(__instance);

    private static void Touch(Player p)
    {
        lock (gate)
        {
            lastActive[p.Guid.Full] = DateTime.UtcNow;
            warnedAt.Remove(p.Guid.Full);
        }
    }

    private static void Tick()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var now = DateTime.UtcNow;
        var online = PlayerManager.GetAllOnline(); // verified: PlayerManager.GetAllOnline() returns List<Player>
        lock (gate)
        {
            var ids = online.Select(p => p.Guid.Full).ToHashSet();
            foreach (var k in lastActive.Keys.Where(k => !ids.Contains(k)).ToList()) { lastActive.Remove(k); warnedAt.Remove(k); }
            foreach (var p in online)
            {
                var id = p.Guid.Full;
                var s = p.Session;
                if (s == null) continue;
                // Staff, combat stance and PK timers count as active.
                if (s.AccessLevel >= AccessLevel.Advocate || p.CombatMode != CombatMode.NonCombat || p.PKTimerActive)
                {
                    lastActive[id] = now; warnedAt.Remove(id); continue;
                }
                if (!lastActive.TryGetValue(id, out var last)) { lastActive[id] = now; continue; }
                if (warnedAt.TryGetValue(id, out var w))
                {
                    if ((now - w).TotalSeconds >= cfg.GraceSeconds)
                    {
                        warnedAt.Remove(id); lastActive.Remove(id);
                        ModManager.Log($"[IdleKick] logging off {p.Name} (idle)");
                        s.LogOffPlayer(); // verified: Session.LogOffPlayer(bool forceImmediate = false)
                    }
                }
                else if ((now - last).TotalMinutes >= cfg.IdleMinutes)
                {
                    warnedAt[id] = now;
                    s.Network.EnqueueSend(new GameMessageSystemChat(string.Format(cfg.WarnMessage, cfg.GraceSeconds), ChatMessageType.WorldBroadcast));
                }
            }
        }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[IdleKick] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }
}
