using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace HouseEligibilityCheck;

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

    // Free command name (checked against %TEMP%\cmds.txt - no clash with any ACE built-in): housecheck.
    [CommandHandler("housecheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Previews whether you currently meet a mansion's allegiance-rank requirement, using your last-appraised SlumLord.")]
    public static void HandleHouseCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "HouseCheck is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Same last-appraised-object pattern as PriceCheck/VendorStock/CraftForecast:
        // CommandHandlerHelper.GetLastAppraisedObject() is `internal static`
        // (ACE.Server.Command.Handlers, verified) and not visible outside ACE.Server, so we
        // reproduce its body directly: read Player.RequestedAppraisalTarget (public uint?,
        // Player_Properties.cs, verified) and resolve it with Player.FindObject(.., SearchLocations.Everywhere, ..) (public).
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise a mansion's SlumLord first (examine it), then run /housecheck again."); return; }

        var obj = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (obj == null) { Say(session, "Couldn't find your last-appraised object - it may no longer exist."); return; }

        if (obj is not SlumLord slumlord)
        {
            Say(session, "Your last-appraised object isn't a house's SlumLord. Appraise the SlumLord for the mansion you're considering, then run /housecheck again.");
            return;
        }

        // Exact quoted logic from Player_House.cs HandleActionBuyHouse, verified against ACE
        // master (round-trip re-check, 2026-09-22): SlumLord.AllegianceMinLevel is `public int?`
        // (SlumLord.cs, verified public), read here from a different class exactly the way
        // Player_House.cs itself reads it as a bare `slumlord.AllegianceMinLevel`.
        if (slumlord.AllegianceMinLevel == null)
        {
            Say(session, "This mansion has no allegiance-rank requirement.");
            return;
        }

        // Server-property-first, weenie-value-fallback - the exact order the game uses, so this
        // never prints a wrong number on a server that overrides the default (mansion_min_rank).
        // PropertyManager.GetLong is `public static` (ACE.Server.Managers, verified, used
        // identically by SettingsPeek).
        var allegianceMinLevel = PropertyManager.GetLong("mansion_min_rank", -1).Item;
        if (allegianceMinLevel == -1)
            allegianceMinLevel = slumlord.AllegianceMinLevel.Value;

        if (allegianceMinLevel <= 0)
        {
            Say(session, "This mansion has no allegiance-rank requirement.");
            return;
        }

        // Player.Allegiance (public Allegiance) and Player.AllegianceNode (public AllegianceNode)
        // are declared `public` auto-properties in Player_Allegiance.cs (re-verified 2026-09-22,
        // not merely inferred from their bare use inside Player.cs itself - the trap the idea
        // called out for SlumLord.AllegianceMinLevel does not apply here). AllegianceNode.Rank is
        // `public uint` (AllegianceNode.cs, verified). Read directly off the caller's own Player
        // instance, exactly as HandleActionBuyHouse reads `Allegiance` / `AllegianceNode.Rank` on
        // itself - no need for AllegianceManager.GetAllegianceNode(player), which exists for
        // looking up a rank from outside a Player instance (AllegianceRoster,
        // FellowshipShareToggle) and is not required for a player checking their own rank.
        if (player.Allegiance == null || player.AllegianceNode == null || player.AllegianceNode.Rank < allegianceMinLevel)
        {
            var currentRank = player.AllegianceNode?.Rank;
            Say(session, currentRank == null
                ? $"You need allegiance rank {allegianceMinLevel} to buy this mansion. You are not in an allegiance."
                : $"You need allegiance rank {allegianceMinLevel} to buy this mansion. You are rank {currentRank.Value}.");
            return;
        }

        Say(session, $"Eligible: you meet the allegiance-rank requirement (rank {allegianceMinLevel}) for this mansion.");
    }
}
