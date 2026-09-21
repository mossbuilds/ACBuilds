using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;
using Position = ACE.Entity.Position;

namespace StuckRescue;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings cfg = new();
    private static readonly object gate = new();
    private static readonly Dictionary<uint, List<DateTime>> uses = new();

    public override Task OnWorldOpen()
    {
        cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Player p, string msg) =>
        p.Session?.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // "unstick"/"stuck" checked against ACE's built-ins; /unstick is used to be safe.
    [CommandHandler("unstick", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Free yourself if stuck in a wall or the ground.", "")]
    public static void HandleSelf(Session session, params string[] parameters)
    {
        var p = session?.Player;
        if (p == null) return;
        if (!cfg.Enabled) { Say(p, "This command is not enabled."); return; }
        if (p.PKTimerActive || p.CombatMode != CombatMode.NonCombat) { Say(p, "You cannot do that in combat."); return; }
        if ((DateTime.UtcNow - p.LastTeleportTime).TotalSeconds < cfg.MinSecondsSinceTeleport) { Say(p, "You teleported too recently."); return; }
        lock (gate)
        {
            var list = uses.TryGetValue(p.Guid.Full, out var l) ? l : uses[p.Guid.Full] = new();
            var now = DateTime.UtcNow;
            list.RemoveAll(t => (now - t).TotalHours >= 1);
            if (list.Count > 0)
            {
                var left = cfg.CooldownSeconds - (now - list[^1]).TotalSeconds;
                if (left > 0) { Say(p, $"Wait {(int)left + 1}s before using this again."); return; }
            }
            if (cfg.MaxPerHour > 0 && list.Count >= cfg.MaxPerHour) { Say(p, "Hourly limit reached."); return; }
            list.Add(now);
        }
        Say(p, Rescue(p, true));
    }

    [CommandHandler("unstickplayer", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, "Free a stuck online player.", "<name>")]
    public static void HandleAdmin(Session session, params string[] parameters)
    {
        var target = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters));
        if (target == null) { if (session?.Player != null) Say(session.Player, "Player not online."); return; }
        var msg = Rescue(target, false);
        if (session?.Player != null) Say(session.Player, $"{target.Name}: {msg}");
        else ModManager.Log($"[StuckRescue] {target.Name}: {msg}");
    }

    /// <summary>Picks a spot and queues the teleport (safe from any thread).</summary>
    private static string Rescue(Player p, bool self)
    {
        var loc = p.Location;
        Position? dest = null;
        if (!loc.Indoors) dest = FindOutdoor(loc);
        if (dest == null)
        {
            if (!cfg.FallbackToSanctuary) return "No safe spot nearby.";
            dest = p.Sanctuary;
            if (dest == null) return "No safe spot nearby and no sanctuary attuned.";
        }
        WorldManager.ThreadSafeTeleport(p, dest);
        ModManager.Log($"[StuckRescue] {p.Name} {(self ? "self" : "admin")} rescue");
        return "Moving you to a safe spot.";
    }

    /// <summary>Nearest walkable outdoor point on rings up to MaxDistance (ring 0 = straight up from under terrain).</summary>
    private static Position? FindOutdoor(Position origin)
    {
        var step = Math.Max(1f, cfg.StepDistance);
        for (float r = 0; r <= cfg.MaxDistance; r += step)
        {
            var n = r == 0 ? 1 : 8;
            for (var i = 0; i < n; i++)
            {
                var a = i * MathF.PI * 2 / n;
                var c = new Position(origin);
                c.PositionX = origin.PositionX + r * MathF.Cos(a);
                c.PositionY = origin.PositionY + r * MathF.Sin(a);
                c.LandblockId = new LandblockId(c.GetCell());
                if (c.Indoors) continue;
                c.PositionZ = c.GetTerrainZ() + cfg.ZOffset;
                if (!c.IsWalkable()) continue;
                return c;
            }
        }
        return null;
    }
}
