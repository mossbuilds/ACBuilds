using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace NetWorthCheck;

/// <summary>
/// Player command: /networth
/// Sums the flat WorldObject.Value across everything the caller is carrying (Player.Inventory) and wearing
/// (Player.EquippedObjects), reporting pack total, worn total, and the combined total.
/// Read-only: never writes Value, never calls a vendor/sale-price path (that is the separate, already-shipped
/// PriceCheck / idea 49, which uses Vendor.GetBuyCost instead of the item's own flat Value).
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const string Tag = "[NetWorthCheck]";

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer.Settings;
        ModManager.Log($"{Tag} ready: /networth (player, read-only)");
        return base.OnWorldOpen();
    }

    /// <summary>
    /// Adds up Value * (StackSize ?? 1) for every item in a collection, skipping items with no Value set at all
    /// (a null Value means "not priced", not zero - counting it as zero would understate the total silently).
    /// Returns the running total plus how many items were skipped for having no Value.
    /// </summary>
    private static (long total, int skipped) SumValue(IEnumerable<WorldObject> items)
    {
        long total = 0;
        var skipped = 0;
        foreach (var item in items)
        {
            if (item.Value is not int value)
            {
                skipped++;
                continue;
            }
            total += (long)value * (item.StackSize ?? 1);
        }
        return (total, skipped);
    }

    [CommandHandler("networth", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Reports the total flat Value of everything you are carrying and wearing.")]
    public static void HandleNetWorth(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;

        // Player.Inventory (from Container) - main pack + side-pack contents already flattened one level via GetInventoryItem's
        // recursion pattern elsewhere, but Inventory itself only holds top-level pack items and side containers; sum both:
        // the side container's own Value already reflects everything inside it (Container.TryAddToInventory rolls a
        // contained item's Value up into the container's own Value as items are added), so summing Inventory.Values
        // directly is correct and does not need to recurse into side packs separately.
        var (packTotal, packSkipped) = SumValue(player.Inventory.Values);

        // Player.EquippedObjects (from Creature) - everything currently worn/wielded.
        var (wornTotal, wornSkipped) = SumValue(player.EquippedObjects.Values);

        var combined = packTotal + wornTotal;
        var skippedTotal = packSkipped + wornSkipped;

        session.Network.EnqueueSend(new GameMessageSystemChat(
            $"Net worth: {combined:N0} (pack {packTotal:N0} + worn {wornTotal:N0}).", ChatMessageType.Broadcast));

        if (skippedTotal > 0)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat(
                $"{skippedTotal} item(s) have no Value set and were excluded from the total (not counted as zero).",
                ChatMessageType.Broadcast));
        }
    }
}
