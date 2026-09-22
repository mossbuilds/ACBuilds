using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PriceCheck;

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

    // Searches the caller's own inventory only (top-level pack, side (sub)containers, and equipped
    // items) - never another player's. Container.Inventory / Player.EquippedObjects are public
    // (Container.cs, Creature_Equipment.cs, verified). Recurses into side containers the same way
    // Container.GetInventoryItemsOfWeenieClass does, but matches on the item's display Name instead
    // of its weenie class.
    private static WorldObject? FindInOwnInventoryByName(Player player, string name)
    {
        WorldObject? partial = null;

        bool Matches(WorldObject wo)
        {
            if (string.Equals(wo.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            if (partial == null && wo.Name != null && wo.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                partial = wo;
            return false;
        }

        WorldObject? Search(Container container)
        {
            foreach (var item in container.Inventory.Values)
            {
                if (Matches(item)) return item;
                if (item is Container sub)
                {
                    var found = Search(sub);
                    if (found != null) return found;
                }
            }
            return null;
        }

        var exact = Search(player);
        if (exact != null) return exact;

        foreach (var item in player.EquippedObjects.Values)
        {
            if (Matches(item)) return item;
        }

        return partial;
    }

    // Same formula as Vendor.GetBuyCost(int? value, ItemType? itemType) - verified Vendor.cs,
    // ACE.Server.WorldObjects, lines ~591-599: vendor pays (buyRate * Value), floored, min 1,
    // except promissory notes which always pay back at 1.0x. We have no specific vendor here
    // (the command targets an item, not a vendor), so the multiplier is a configured assumption,
    // not a live vendor's own BuyPrice property - real vendors vary and we say so in the reply.
    private static int EstimateVendorSellPrice(WorldObject item, double buyRateMultiplier)
    {
        var value = item.Value ?? 0;
        var rate = buyRateMultiplier;
        if (item.ItemType == ItemType.PromissoryNote)
            rate = 1.0;

        return Math.Max(1, (int)Math.Floor((float)rate * value + 0.1));
    }

    private static void ReportItem(Session session, WorldObject item, double buyRateMultiplier)
    {
        var value = item.Value ?? 0;
        var est = EstimateVendorSellPrice(item, buyRateMultiplier);

        Say(session, $"--- {item.Name} ---");
        Say(session, $"Base Value: {value:N0}");
        Say(session, $"Estimated vendor sell price: {est:N0} (assumes a {buyRateMultiplier:0.###}x buy rate; real vendors vary and may pay less, or use an alternate currency this does not account for)");
        if (item.StackSize.HasValue && item.StackSize.Value > 1)
            Say(session, $"Stack size: {item.StackSize.Value}");

        var flags = new List<string>();
        if (item.Attuned is AttunedStatus.Attuned or AttunedStatus.Sticky)
            flags.Add(item.Attuned == AttunedStatus.Sticky ? "attuned (sticky)" : "attuned");
        if (item.Bonded is BondedStatus.Bonded or BondedStatus.Sticky or BondedStatus.Destroy)
            flags.Add(item.Bonded.ToString()!.ToLowerInvariant());
        if (item.Retained)
            flags.Add("retained");

        // Player_Commerce.cs (the real sell-to-vendor path, verified) only checks Retained and the
        // separate IsSellable property before refusing a sale; Attuned/Bonded are reported here as
        // informational flags per the brief, not verified as sale-blockers in ACE's own code.
        Say(session, flags.Count > 0 ? $"Flags: {string.Join(", ", flags)}" + (item.Retained ? " - a vendor will refuse this." : "") : "Flags: none - normally sellable.");
    }

    // Free command names (checked against %TEMP%\cmds.txt - no clash with any ACE built-in):
    // pricecheck, worth, appraiseprice.
    [CommandHandler("pricecheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows what an item you hold or last appraised is worth.", "[item name]")]
    public static void HandlePriceCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "PriceCheck is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        WorldObject? item;

        if (parameters.Length == 0)
        {
            // No name given: use the caller's last appraised object. CommandHandlerHelper is
            // `internal static` (ACE.Server.Command.Handlers, verified) and not visible outside
            // ACE.Server, so we reproduce its GetLastAppraisedObject() body directly: it reads
            // Player.RequestedAppraisalTarget (public uint?, Player_Properties.cs, verified) and
            // resolves it with Player.FindObject(.., SearchLocations.Everywhere, ..) (public).
            var targetId = player.RequestedAppraisalTarget;
            if (targetId == null) { Say(session, "Appraise an item first (examine it), or give /pricecheck an item name from your own inventory."); return; }

            item = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
            if (item == null) { Say(session, "Couldn't find your last appraised item - it may no longer exist."); return; }
        }
        else
        {
            var name = string.Join(" ", parameters);
            item = FindInOwnInventoryByName(player, name);
            if (item == null) { Say(session, $"You aren't carrying or wearing anything named \"{name}\"."); return; }
        }

        ReportItem(session, item, cfg.AssumedBuyPriceMultiplier);
    }
}
