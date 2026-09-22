using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace FellowshipShareToggle;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    // Per-fellowship cooldown, keyed by object reference (Fellowship does not override Equals/GetHashCode,
    // so default reference equality is exactly what we want here - no need for a separate id).
    private static readonly Dictionary<Fellowship, DateTime> LastToggle = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // "fellowshare" is not a built-in ACE command (checked against the built-in list in %TEMP%\cmds.txt;
    // the three built-in fellow-prefixed names are fellow-info, fellow-dist and fellowbuff, all
    // admin/sentinel-only debug or unrelated commands - no clash, and no player-facing command exists
    // anywhere in stock ACE that changes fellowship XP sharing after the fellowship is created).
    //
    // Confirmed gap (raw Entity/Fellowship.cs + WorldObjects/Player_Fellowship.cs, both read directly):
    // `Fellowship.DesiredShareXP` is a public field, but it is only ever assigned once, in the
    // `Fellowship(Player leader, string fellowshipName, bool shareXP)` constructor invoked from
    // `Player.FellowshipCreate`. The only code that recomputes `ShareXP`/`EvenShare` from it afterwards is
    // the private `Fellowship.CalculateXPSharing()`, called internally on membership/leadership changes -
    // there is no public method and no `/fellow*` player command anywhere in ACE that lets a leader flip
    // sharing mid-session; the only existing workaround is disbanding and re-forming the fellowship. This
    // command closes that exact gap: it is a genuine leader-only convenience, not a wrapper around
    // something ACE already exposes.
    [CommandHandler("fellowshare", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
        "Toggles XP sharing for your fellowship without disbanding it (leader only).", "<on|off|status>")]
    public static void HandleFellowShare(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "FellowshipShareToggle is switched off."); return; }

        var fs = p.Fellowship;
        if (fs == null) { Say(session, "You are not in a fellowship."); return; }

        var arg = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

        if (arg == "status")
        {
            Say(session, $"Fellowship XP sharing: desired {(fs.DesiredShareXP ? "on" : "off")}, " +
                $"currently {(fs.ShareXP ? "on" : "off")}{(fs.ShareXP ? (fs.EvenShare ? " (even split)" : " (weighted by level)") : "")}.");
            return;
        }

        if (arg != "on" && arg != "off")
        {
            Say(session, "Usage: /fellowshare <on|off|status>");
            return;
        }

        // FellowshipLeaderGuid (uint field, verified in Fellowship.cs) - leader-only, same check
        // Player_Fellowship.cs uses for openness/lock changes (HandleActionFellowshipChangeOpenness).
        if (p.Guid.Full != fs.FellowshipLeaderGuid)
        {
            Say(session, "Only the fellowship leader can change XP sharing.");
            return;
        }

        var desired = arg == "on";

        if (fs.DesiredShareXP == desired)
        {
            Say(session, $"XP sharing is already {(desired ? "on" : "off")}.");
            return;
        }

        var cooldown = TimeSpan.FromSeconds(Math.Max(0, cfg.CooldownSeconds));
        if (cooldown > TimeSpan.Zero && LastToggle.TryGetValue(fs, out var last) && DateTime.UtcNow - last < cooldown)
        {
            var remaining = (int)Math.Ceiling((cooldown - (DateTime.UtcNow - last)).TotalSeconds);
            Say(session, $"You changed XP sharing too recently. Try again in {remaining}s.");
            return;
        }

        var previousDesired = fs.DesiredShareXP;
        fs.DesiredShareXP = desired;

        try
        {
            // CalculateXPSharing() and UpdateAllMembers() are both private (verified, Entity/Fellowship.cs) -
            // no public method recomputes sharing from DesiredShareXP outside of membership/leadership-change
            // events ACE triggers internally. Harmony's Traverse reflects into them; if a future ACE rename
            // makes either call throw, the catch block below reverts DesiredShareXP to what it was before this
            // command ran, so a failed toggle can never leave the field drifted out of sync with the actual
            // computed ShareXP/EvenShare state (which only CalculateXPSharing produces).
            var t = Traverse.Create(fs);
            t.Method("CalculateXPSharing").GetValue();
            t.Method("UpdateAllMembers").GetValue();

            LastToggle[fs] = DateTime.UtcNow;

            Say(session, $"XP sharing set to {(desired ? "on" : "off")} for your fellowship.");
            if (desired && !fs.ShareXP)
            {
                // DesiredShareXP is honored only within ACE's own level-spread rule; explain the mismatch
                // rather than leaving the player thinking the toggle silently failed.
                Say(session, "Note: sharing is still off - your fellowship's level spread is outside ACE's own sharing range.");
            }
        }
        catch (Exception e)
        {
            fs.DesiredShareXP = previousDesired; // revert - the recompute never ran, so the flag must not have changed either
            ModManager.Log($"[FellowshipShareToggle] reflection call failed, no change applied: {e.Message}");
            Say(session, "Could not change XP sharing (internal error, logged).");
        }
    }
}
