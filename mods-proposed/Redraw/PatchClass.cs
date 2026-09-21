using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace Redraw;

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

    // redraw/refresh/resync/fixview/redrawplayer checked against %TEMP%\cmds.txt: none are built-ins.
    [CommandHandler("redraw", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Re-send nearby objects to your client if things look invisible.", "")]
    public static void HandleSelf(Session session, params string[] parameters)
    {
        var p = session?.Player;
        if (p == null) return;
        if (!cfg.Enabled) { Say(p, "This command is not enabled."); return; }
        if (p.PKTimerActive) { Say(p, "You cannot do that in combat."); return; }
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
        Resend(p);
    }

    [CommandHandler("redrawplayer", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, "Re-send nearby objects to an online player's client.", "<name>")]
    public static void HandleAdmin(Session session, params string[] parameters)
    {
        var target = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters));
        if (target == null) { if (session?.Player != null) Say(session.Player, "Player not online."); return; }
        Resend(target);
        if (session?.Player != null) Say(session.Player, $"{target.Name}: redraw queued.");
        else ModManager.Log($"[Redraw] {target.Name}: redraw queued");
    }

    /// <summary>Queued on the player's own action queue (safe from any thread).</summary>
    private static void Resend(Player p)
    {
        var chain = new ActionChain();
        chain.AddAction(p, () =>
        {
            try
            {
                var phys = p.PhysicsObj;
                if (phys?.ObjMaint == null) return;
                // Objects the server already tracks for this player; enqueue_objs sends CreateObject
                // (player.TrackObject) to THIS client only. No known-object add/remove, no teleport.
                var objs = phys.ObjMaint.GetKnownObjectsValues();
                if (cfg.MaxObjects > 0 && objs.Count > cfg.MaxObjects) objs = objs.Take(cfg.MaxObjects).ToList();
                phys.enqueue_objs(objs);
                // Fresh position for the player; broadcast to nearby players (normal movement message).
                p.SendUpdatePosition();
                Say(p, $"Refreshed {objs.Count} nearby objects.");
                ModManager.Log($"[Redraw] {p.Name} resent {objs.Count} objects");
            }
            catch (Exception e) { ModManager.Log($"[Redraw] failed: {e.Message}"); }
        });
        chain.EnqueueChain();
    }
}
