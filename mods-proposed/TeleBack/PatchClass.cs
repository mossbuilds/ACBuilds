using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;
using Position = ACE.Entity.Position;

namespace TeleBack;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static readonly Dictionary<uint, List<Position>> history = new();
    // guids whose next Teleport is our own /teleback (not recorded)
    private static readonly HashSet<uint> undoing = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    // Player.Teleport(Position, bool) - public, Player_Location.cs (ACE.Server.WorldObjects).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.Teleport), new Type[] { typeof(Position), typeof(bool) })]
    public static void PreTeleport(Player __instance)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var s = __instance.Session;
        if (s == null || s.AccessLevel < cfg.MinAccess) return;
        var loc = __instance.Location;
        if (loc == null) return;
        uint id = __instance.Guid.Full;
        lock (gate)
        {
            if (undoing.Remove(id)) return;
            if (!history.TryGetValue(id, out var l)) history[id] = l = new();
            l.Add(new Position(loc));
            while (l.Count > Math.Max(1, cfg.HistorySize)) l.RemoveAt(0);
            if (history.Count > 200)
                foreach (var k in history.Keys.Where(k => k != id).Take(100).ToList()) history.Remove(k);
        }
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // teleback/back/undotele are not among the built-in commands.
    [CommandHandler("teleback", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0, "Return to where you were before your last teleport.", "[n]")]
    public static void HandleBack(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "TeleBack is off."); return; }
        var p = session.Player;
        if (session.AccessLevel < cfg.MinAccess) return;
        int n = 1;
        if (parameters.Length > 0 && (!int.TryParse(parameters[0], out n) || n < 1)) { Say(session, "Usage: /teleback [n]"); return; }
        Position? dest = null;
        lock (gate)
        {
            if (!history.TryGetValue(p.Guid.Full, out var l) || l.Count == 0) { Say(session, "No previous position stored."); return; }
            if (n > l.Count) n = l.Count;
            dest = l[l.Count - n];
            l.RemoveRange(l.Count - n, n);
            undoing.Add(p.Guid.Full);
        }
        Say(session, $"Returning to {dest.ToLOCString()}");
        WorldManager.ThreadSafeTeleport(p, dest);
    }
}
