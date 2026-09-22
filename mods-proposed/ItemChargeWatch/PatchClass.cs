using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ItemChargeWatch;

/// <summary>
/// Player command: /manawatch
/// Lists every equipped item that runs on mana (has ItemMaxMana set), sorted lowest-percent-remaining first,
/// so a caster can see at a glance what is about to run dry mid-fight instead of discovering it only when a
/// cast silently fails. Read-only: never writes ItemCurMana/ItemMaxMana, never touches any item.
///
/// Verified against ACE master (Source/ACE.Server/WorldObjects/ManaStone.cs), re-fetched for this build:
/// - ManaStone.cs reads and writes ItemCurMana/ItemMaxMana on an *external* target/item of type WorldObject from
///   ManaStone's own class body, e.g. `target.ItemCurMana.HasValue`, `target.ItemMaxMana.Value`,
///   `item.ItemCurMana += adjustedRation` inside `player.EquippedObjects.Values.Where(k => k.ItemCurMana.HasValue
///   && k.ItemMaxMana.HasValue ...)`. This confirms both properties are plain public get/set on WorldObject,
///   readable from a different declaring class - satisfying this repo's external-access verification rule.
/// - player.EquippedObjects.Values is the same public collection (Creature_Equipment.cs, base of Player) already
///   used identically by ManaStone.cs itself and by the already-shipped NetWorthCheck (idea's own reference).
/// </summary>
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

    [CommandHandler("manawatch", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Lists your equipped mana-driven items' current/max mana, lowest-first.")]
    public static void HandleManaWatch(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Item charge watch is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Only items that actually run on mana (ItemMaxMana set); a null/zero max means the item isn't
        // mana-driven at all and would otherwise show a meaningless 0/0 or divide-by-zero percent.
        var manaItems = player.EquippedObjects.Values
            .Where(i => i.ItemMaxMana.HasValue && i.ItemMaxMana.Value > 0)
            .Select(i => (item: i, cur: i.ItemCurMana ?? 0, max: i.ItemMaxMana!.Value))
            .Select(t => (t.item, t.cur, t.max, pct: (int)Math.Round(100.0 * t.cur / t.max)))
            .OrderBy(t => t.pct)
            .ToList();

        if (manaItems.Count < 1)
        {
            Reply(session, "You have no equipped items that run on mana.");
            return;
        }

        Reply(session, $"Item charge watch ({manaItems.Count} item(s), lowest first):");
        foreach (var (item, cur, max, pct) in manaItems)
        {
            var flag = pct <= cfg.WarnPercent ? " [LOW]" : "";
            Reply(session, $"  {item.Name}: {cur:N0}/{max:N0} ({pct}%){flag}");
        }
    }
}
