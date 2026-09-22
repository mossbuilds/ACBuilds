using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PkStatusInfo;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Reply(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Verified against ACE master (Source/ACE.Server/WorldObjects/Player_Combat.cs, WorldObject_Properties.cs):
    // - PlayerKillerStatus: public property on WorldObject (WorldObject_Properties.cs), inherited enum PK/PKLite/NPK/Free/etc.
    // - LastPkAttackTimestamp: public double property (PropertyFloat.LastPkAttackTimestamp), Unix time of last PK attack.
    // - PKTimerActive: public bool property = IsPKType && (now - LastPkAttackTimestamp) < PropertyManager.GetLong("pk_timer").Item.
    //   NOTE: this is the "pk_timer" config key, not "pk_respite_timer" - pk_respite_timer is a separate double key read
    //   elsewhere (Player_Death.cs, PlayerManager.cs) for the post-death respite message/check, not for this live flag.
    //   The idea entry's "pk_timer/pk_respite_timer" pairing was imprecise; PKTimerActive itself only ever reads pk_timer.
    // - PKLogoutActive: public bool property = IsPKType && (now - LastPkAttackTimestamp) < PKLogoffTimer.TotalSeconds.
    // - Static timer field is named PKLogoffTimer (TimeSpan, 2 minutes), NOT "PKLogoutTimer" as the idea entry spelled it -
    //   the idea's field name was wrong; PKLogoffTimer is the real, public, static member and is what this command reads.
    // Dropped from the idea: PkTimestamp (exists, public double, but nothing in Player_Combat.cs computes an active/remaining
    // window from it - only LastPkAttackTimestamp feeds PKTimerActive/PKLogoutActive) and pk_new_character_grace_period
    // (a real, documented PropertyManager key, but not referenced anywhere in Player.cs/Player_Combat.cs - nothing here
    // actually adds it to a timestamp, so a "grace period remaining" number would be invented, not read). Showing either
    // would be guessing at a number the client can't otherwise see, which is exactly what this mod exists to avoid.
    [CommandHandler("pkstatus", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows your own PK status and remaining PK-timer/logout-freeze window.")]
    public static void HandlePkStatus(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "PK status is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        Reply(session, $"PK status: {player.PlayerKillerStatus}");

        var now = ACE.Common.Time.GetUnixTime();
        var sinceAttack = now - player.LastPkAttackTimestamp;

        if (player.PKTimerActive)
        {
            var pkTimerSeconds = PropertyManager.GetLong("pk_timer").Item;
            var remaining = pkTimerSeconds - sinceAttack;
            Reply(session, $"PK timer active: ~{Math.Max(0, (long)remaining)}s remaining before you can be flagged non-player-killer again.");
        }
        else
        {
            Reply(session, "PK timer: not active.");
        }

        if (player.PKLogoutActive)
        {
            var remaining = Player.PKLogoffTimer.TotalSeconds - sinceAttack;
            Reply(session, $"Logout freeze active: ~{Math.Max(0, (long)remaining)}s remaining (logging out now will not protect you from a recent PK attacker).");
        }
        else
        {
            Reply(session, "Logout freeze: not active.");
        }
    }
}
