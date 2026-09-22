using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ItemSetPreview;

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

    // Free command name (checked against %TEMP%\cmds.txt, 327 built-ins - no clash): itemset.
    [CommandHandler("itemset", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Previews the equipment-set spells of an item you hold or last appraised.", "[item name]")]
    public static void HandleItemSet(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ItemSetPreview is not available."); return; }

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
            if (targetId == null) { Say(session, "Appraise an item first (examine it), or give /itemset an item name from your own inventory."); return; }

            item = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
            if (item == null) { Say(session, "Couldn't find your last appraised item - it may no longer exist."); return; }
        }
        else
        {
            var name = string.Join(" ", parameters);
            item = FindInOwnInventoryByName(player, name);
            if (item == null) { Say(session, $"You aren't carrying or wearing anything named \"{name}\"."); return; }
        }

        ReportItem(session, item);
    }

    // Searches the caller's own inventory only (top-level pack, side (sub)containers, and equipped
    // items) - never another player's. Container.Inventory / Player.EquippedObjects are public
    // (Container.cs, Creature_Equipment.cs, verified). Same helper as PriceCheck.
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

    // WorldObject.EquipmentSetId / HasItemSet / ItemLevel / HasItemLevel / GetSpellSetAll all
    // confirmed public in Source/ACE.Server/WorldObjects/WorldObject_Set.cs (fetched in full this
    // round). Deliberately reports the all-tiers spell list via the static GetSpellSetAll rather
    // than the current-tier GetSpellSet(List<WorldObject>, int), which needs every other equipped
    // piece of the same set plus branching on ItemXpStyle to get the "current tier" number right -
    // out of scope for a simple read-only preview, per the idea's own risk note.
    private static void ReportItem(Session session, WorldObject item)
    {
        Say(session, $"--- {item.Name} ---");

        if (!item.HasItemSet)
        {
            Say(session, "This item is not part of an equipment set.");
            return;
        }

        Say(session, $"Equipment set: {item.EquipmentSetId}");

        if (item.HasItemLevel)
            Say(session, $"Item level: {item.ItemLevel}");

        var spells = WorldObject.GetSpellSetAll(item.EquipmentSetId!.Value);

        if (spells.Count == 0)
        {
            Say(session, "No set spells are defined for this set in the client data.");
            return;
        }

        Say(session, $"Set spells (all tiers, {spells.Count} total):");
        foreach (var spell in spells)
            Say(session, $"  {spell.Name}");
    }
}
