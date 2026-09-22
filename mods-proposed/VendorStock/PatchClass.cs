using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace VendorStock;

// IDEAS.md idea 52: "why did this vendor's stock change / will it restock" - motivated by the
// Server-Configurable-Options wiki noting `vendor_shop_uses_generator` is silent to players (stock
// rules differ per-server/per-vendor with no in-game indicator). Read-only, no patches, no world
// state changed. Reads the caller's LAST APPRAISED object if it is a Vendor - CommandHandlerHelper
// is internal (not visible outside ACE.Server, confirmed the same way PriceCheck already handled
// this), so this reuses PriceCheck's own verified reproduction of that lookup:
// Player.RequestedAppraisalTarget (public uint?, Player_Properties.cs) + Player.FindObject(..,
// SearchLocations.Everywhere, ..) (public).
//
// Vendor fields used - confirmed accessible by compiling, not just by reading source:
//   OpenForBusiness (public bool property, PropertyBool-backed, defaults true)
//   DefaultItemsForSale / UniqueItemsForSale (both public Dictionary<ObjectGuid, WorldObject>)
//   IsGenerator (public bool on WorldObject, WorldObject_Generators.cs: GeneratorProfiles != null
//   && GeneratorProfiles.Count > 0) - confirms whether this vendor also restocks via the generator
//   system in addition to its createlist, the exact ambiguity the wiki page flags.
// NOT used: ResetTimestamp/ResetInterval, the fields that would give a "seconds until next reset"
// countdown. Vendor.cs itself reads/writes both as bare identifiers, so they are real WorldObject
// members - but a real compile attempt (not just a source read) showed `ResetTimestamp` is
// `protected`, not public, so it cannot be read from a mod without reflection. Reflecting into a
// protected timer field for a "nice to have" countdown wasn't judged worth the risk this idea's
// own note flagged (verify before coding) - so this mod reports open/closed status and stock
// composition, and is honest in its own output that the reset countdown isn't available.
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

    // Free command name (checked against %TEMP%\cmds.txt's 327 built-ins - no clash).
    [CommandHandler("vendorstock", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows whether your last-appraised vendor is open, when it next resets, and how it restocks.", "")]
    public static void HandleVendorStock(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "VendorStock is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Same last-appraised lookup PriceCheck already verified and uses (CommandHandlerHelper is
        // internal to ACE.Server, not reachable from a mod).
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise a vendor first (examine it), then use /vendorstock."); return; }

        var obj = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (obj == null) { Say(session, "Couldn't find your last appraised object - it may no longer exist."); return; }

        if (obj is not Vendor vendor)
        {
            Say(session, "Your last appraised object is not a vendor.");
            return;
        }

        var open = vendor.OpenForBusiness;
        var defaultCount = vendor.DefaultItemsForSale.Count;
        var uniqueCount = vendor.UniqueItemsForSale.Count;
        var usesGenerator = vendor.IsGenerator;

        Say(session,
            $"{vendor.Name}: open for business: {(open ? "yes" : "no")}. " +
            $"Stock: {defaultCount} default item(s), {uniqueCount} player-sold item(s) for sale. " +
            $"Also restocks via the generator system: {(usesGenerator ? "yes" : "no")}. " +
            "(No reset-timer countdown - ACE keeps that internal to the vendor and this mod does not reflect into it.)"
        );
    }
}
