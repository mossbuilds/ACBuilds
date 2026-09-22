# ItemSetPreview

Idea 63 (Round 10). Read-only player command that previews an item's equipment-set
bonus spells.

## What it does

`/itemset [item name]` - with no argument, reads the caller's last-appraised object
(same pattern as PriceCheck/VendorStock/CraftForecast/HouseEligibilityCheck: reads
`Player.RequestedAppraisalTarget` and resolves it with `Player.FindObject(...,
SearchLocations.Everywhere, ...)`, since `CommandHandlerHelper` is `internal` and not
visible outside `ACE.Server`). With an argument, searches the caller's own inventory
(top-level pack, side containers, equipped items) by name, exact match preferred over
partial.

For the resolved item, if `WorldObject.HasItemSet` is true, prints:

- the equipment set id (`WorldObject.EquipmentSetId`)
- the item's own level, if it has one (`WorldObject.ItemLevel`, gated by `HasItemLevel`)
- every spell the set can grant at any tier (`WorldObject.GetSpellSetAll(EquipmentSet)`,
  a static helper - no other equipped pieces needed)

If the item is not part of a set, says so and stops.

## What it does NOT do

- Does not compute the player's **current** tier of the set (which spells are
  actually active right now). That needs `WorldObject.GetSpellSet(List<WorldObject>
  setItems, int levelDiff)`, which requires collecting every other equipped item
  sharing the same `EquipmentSetId` and branching on `ItemXpStyle` (0 = count-based
  tiers, >0 = level-sum-based) to get the tier number right. Out of scope for a
  simple preview; mirrored from the idea's own risk note rather than guessed at.
- Never modifies the item or the player. No patches, no state, nothing written to
  disk.
- Not a substitute for the client's own set-item tooltip; this is a spoken/system-chat
  preview only.

## Risks

None identified for the all-tiers listing this mod implements. All named members
(`EquipmentSetId`, `HasItemSet`, `ItemLevel`, `HasItemLevel`, `GetSpellSetAll`) were
re-verified public by direct fetch of
`Source/ACE.Server/WorldObjects/WorldObject_Set.cs` from ACEmulator/ACE master this
round (partial class `ACE.Server.WorldObjects.WorldObject`), and `Spell.Name` was
verified public in `Source/ACE.Server/Entity/SpellProperties.cs`. No reflection
fallback was needed.

## Settings

None beyond the standard `Enabled` (default `false`).

## How to test

1. `mods-proposed/check-mod.sh ItemSetPreview` to confirm it compiles.
2. To try it live (not part of this task - source only): copy the built DLL + Meta.json
   into a local ACE server's `Mods/ItemSetPreview/` folder, flip `Enabled: true` in
   Meta.json (or its runtime settings file), restart/hot-reload, then `/itemset` after
   appraising a known set item (e.g. an Olthoi Armor piece), and `/itemset <name>` for
   an unappraised one in inventory.

## How to enable later

Off by default (`Enabled: false` in Meta.json and `Settings.json`). Flip `Enabled` to
`true` and hot-reload or restart to turn it on; no other configuration needed.
