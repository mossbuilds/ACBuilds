using ACE.Common;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace FellowshipRejoinWindow;

// "fellowlock" is not a built-in ACE command (checked against the built-in list in %TEMP%\cmds.txt - no clash).
// Distinct from the already-shipped FellowshipShareToggle (idea 51, XP-sharing only) and FellowshipPulse
// (idea 44, in-range status only): the only in-game signal a fellowship lock exists is a single broadcast
// line sent once, at the moment of locking (Fellowship.UpdateLock), with no way to check the state afterward.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    // Fellowship.UpdateLock (Entity/Fellowship.cs, verified) sets this only via emote, never a player action;
    // when it locks it clears DepartedMembers and stamps FellowshipLocks[lockName] with the lock time.
    private const int RejoinWindowSeconds = 600;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // Player-only, own fellowship only: no target-player/admin variant, no other fellowship is ever readable.
    // Read-only, no files, no world objects, no patches (this command never calls AddFellowshipMember/UpdateLock).
    [CommandHandler("fellowlock", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows whether your fellowship is locked, and your remaining seconds in the 600-second rejoin grace window if you recently left one.", "")]
    public static void HandleFellowLock(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "FellowshipRejoinWindow is switched off."); return; }

        try
        {
            // Player.Fellowship (ACE.Server.WorldObjects, verified: used the same way by FellowshipPulse) is
            // null if the caller is not in a fellowship.
            var fs = p.Fellowship;
            if (fs == null) { Say(session, "You are not in a fellowship."); return; }

            // Fellowship.IsLocked (public bool, verified) is set only through an emote (UpdateLock), never by
            // a player action.
            if (!fs.IsLocked)
            {
                Say(session, "Your fellowship is not locked.");
                return;
            }

            // Fellowship.DepartedMembers (public Dictionary<uint, int>, verified) maps a departed member's
            // guid to the Unix timestamp (int) of when they left, recorded by QuitFellowship only while
            // IsLocked is true. If the caller's own guid is not present, they have not left this locked
            // fellowship since it last locked (DepartedMembers is cleared each time UpdateLock re-locks it).
            if (!fs.DepartedMembers.TryGetValue(p.Guid.Full, out var timeDeparted))
            {
                Say(session, "Your fellowship is locked. You are currently a member, so this does not affect you.");
                return;
            }

            // Same math as Fellowship.AddFellowshipMember's own eligibility check (Time.GetDateTimeFromTimestamp
            // + AddSeconds(600), verified in Fellowship.cs) so this reports exactly what a recruit attempt would see.
            var timeLimit = Time.GetDateTimeFromTimestamp(timeDeparted).AddSeconds(RejoinWindowSeconds);
            var remaining = (timeLimit - DateTime.UtcNow).TotalSeconds;

            if (remaining <= 0)
                Say(session, "Your fellowship is locked. You left it, but your 600-second rejoin window has expired - you can no longer be recruited back in.");
            else
                Say(session, $"Your fellowship is locked. You left it and have {Math.Ceiling(remaining):F0} second(s) left to be recruited back in.");
        }
        catch (Exception e)
        {
            ModManager.Log($"[FellowshipRejoinWindow] {e.Message}");
        }
    }
}
