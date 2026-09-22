using System;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace TinkerHistory;

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

    // Free command name (checked against %TEMP%\cmds.txt, 327 built-ins - no clash): tinkerhistory.
    [CommandHandler("tinkerhistory", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Lists which materials have already been tinkered into an item you hold or last appraised, and how many times each.", "[item name]")]
    public static void HandleTinkerHistory(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "TinkerHistory is not available."); return; }

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
            // Same pattern as ItemSetPreview/PriceCheck/VendorStock/CraftForecast/HouseEligibilityCheck.
            var targetId = player.RequestedAppraisalTarget;
            if (targetId == null) { Say(session, "Appraise an item first (examine it), or give /tinkerhistory an item name from your own inventory."); return; }

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
    // (Container.cs, Creature_Equipment.cs, verified). Same helper as ItemSetPreview/PriceCheck.
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

    // WorldObject.TinkerLog (public string property backed by PropertyString.TinkerLog, confirmed
    // in Source/ACE.Server/WorldObjects/WorldObject_Properties.cs, line ~3080: `public string
    // TinkerLog { get => GetProperty(PropertyString.TinkerLog); set { ... } }`) is a comma-separated
    // string of MaterialType names appended on each successful tinker. The dedicated parser
    // ACE.Server.Entity.TinkerLog (public class, Source/ACE.Server/Entity/TinkerLog.cs, fetched in
    // full: `public TinkerLog(string csv)` builds `public List<MaterialType> Tinkers`, and
    // `public int NumTinkers(MaterialType type)` counts occurrences) does the parsing for us.
    // This only reports the raw counts ACE already keeps - it does not compute or claim to show
    // any remaining workmanship bonus, since the diminishing-returns formula that consumes this
    // log was not read this round (out of scope per the idea's own risk note).
    private static void ReportItem(Session session, WorldObject item)
    {
        Say(session, $"--- {item.Name}: tinker history ---");

        var raw = item.TinkerLog;

        if (string.IsNullOrEmpty(raw))
        {
            Say(session, "This item has never been tinkered.");
            return;
        }

        var log = new ACE.Server.Entity.TinkerLog(raw);

        if (log.Tinkers.Count == 0)
        {
            Say(session, "This item has never been tinkered.");
            return;
        }

        Say(session, $"{log.Tinkers.Count} successful tinker(s) recorded:");

        foreach (var material in Enum.GetValues<MaterialType>())
        {
            var count = log.NumTinkers(material);
            if (count > 0)
                Say(session, $"  {material}: {count}");
        }
    }
}
