using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace FellowshipPulse;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    private static string PctOf(ACE.Server.WorldObjects.Entity.CreatureVital vital) =>
        $"{Math.Round(vital.Percent * 100.0)}%";

    // "fellow" is not a built-in ACE command (checked against the built-in list in %TEMP%\cmds.txt; the
    // built-ins fellow-info, fellow-dist and fellowbuff are separate names, no clash). ACE ships two
    // admin/sentinel-only fellowship debug commands (fellow-dist, fellow-info); this is the ordinary-player
    // equivalent for the caller's own fellowship. Player-only, own fellowship only: no target-player/admin
    // variant, no other fellowship is ever readable. Read-only, no files, no world objects.
    [CommandHandler("fellow", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows your own fellowship at a glance.", "")]
    public static void HandleFellow(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "FellowshipPulse is switched off."); return; }

        try
        {
            // Player.Fellowship (ACE.Server.WorldObjects, verified: used the same way by MentorRank) is null
            // if the caller is not in a fellowship.
            var fs = p.Fellowship;
            if (fs == null) { Say(session, "You are not in a fellowship."); return; }

            // Fellowship.GetFellowshipMembers() (ACE.Server.Entity, verified in Fellowship.cs) returns only
            // members whose Player/Session are currently live - ACE drops a member from the fellowship's own
            // roster once they log off (ProcessDropList), so every entry returned here is online.
            var members = fs.GetFellowshipMembers();

            // Fellowship.WithinRange(Player player, bool includeSelf = false) (verified) returns the fellows
            // that are close enough to this player for XP/loot sharing (radar range, or the "fellow_kt_landblock"
            // same-landblock rule) - the mod's own "in range of you" column.
            var inRange = new HashSet<uint>(fs.WithinRange(p, includeSelf: true).Select(f => f.Guid.Full));

            // FellowshipLeaderGuid (uint field, verified) plus PlayerManager lookups for a display name.
            var leaderName = PlayerManager.GetOnlinePlayer(fs.FellowshipLeaderGuid)?.Name
                ?? PlayerManager.GetOfflinePlayer(fs.FellowshipLeaderGuid)?.Name
                ?? "(unknown)";

            Say(session, $"--- Fellowship: {fs.FellowshipName} ---");
            Say(session, $"Leader: {leaderName}. Members online: {members.Count}.");
            // ShareXP/EvenShare (bool fields, verified): ShareXP is whether XP sharing is active right now;
            // EvenShare is ACE's own even-split rule (true once every fellow is level 50+, or all are within
            // 5 levels of the leader) rather than a weighted share.
            Say(session, $"XP sharing: {(fs.ShareXP ? "on" : "off")}{(fs.ShareXP ? (fs.EvenShare ? ", even split" : ", weighted by level") : "")}.");

            foreach (var m in members.Values.OrderByDescending(m => m.Guid.Full == fs.FellowshipLeaderGuid).ThenBy(m => m.Name))
            {
                var tag = m.Guid.Full == fs.FellowshipLeaderGuid ? " (leader)" : "";
                var range = inRange.Contains(m.Guid.Full) ? "in range" : "out of range";
                Say(session, $"  {m.Name}{tag} - level {m.Level ?? 1} - online, {range} - " +
                    $"HP {PctOf(m.Health)} STA {PctOf(m.Stamina)} MANA {PctOf(m.Mana)}");
            }
        }
        catch (Exception e)
        {
            ModManager.Log($"[FellowshipPulse] {e.Message}");
        }
    }
}
