# NetWorthCheck (ACE mod)

Player command that sums the flat `Value` of everything the caller is carrying and wearing.

| Command | Access | What it does |
|---|---|---|
| `/networth` | Player | Reports pack total, worn total, and the combined total of `WorldObject.Value` across `Player.Inventory` and `Player.EquippedObjects`. |

## What it does / doesn't do

- Read-only: never writes `Value`, never mutates any item or the caller.
- Sums `Value` (the item's own flat, database-set worth) - **not** a sale price. That is the already-shipped
  `PriceCheck` (idea 49), which calls `Vendor.GetBuyCost` and can differ from the flat `Value` reported here.
- Stackables are multiplied by `StackSize` explicitly (`Value * (StackSize ?? 1)`) rather than assuming `Value`
  is already the stack total - the property's own declaration gives no hint either way, so this mod does the
  multiplication itself instead of asserting it silently.
- Items with no `Value` set (`null`, common on many weenies) are **excluded from the total, not counted as
  zero**, and the command reports how many were excluded so an incomplete total is never mistaken for a
  real, complete zero.
- Side-pack contents are not summed a second time: `Container.TryAddToInventory` (confirmed this round, `Container.cs`)
  already rolls a contained item's `Value` up into the side pack's own `Value` as items are added/removed, so
  summing `Player.Inventory.Values` directly (which includes the side pack itself as one entry) is correct
  without a separate recursive walk into each side pack.
- No settings; nothing to configure. `AccessLevel.Player`, `RequiresWorld` (must be logged in and in the world).

## Round 12 correction

Round 12 dropped this idea, saying `WorldObject.Value`'s declaration/accessibility could not be located in a
direct fetch of `WorldObject.cs`. This round found it in the file Round 12 didn't check -
`WorldObject_Properties.cs` - and re-verified it directly against a fresh fetch of ACE master
(`ACEmulator/ACE`, `Source/ACE.Server/WorldObjects/WorldObject_Properties.cs`, line 1221):

```csharp
public int? Value
{
    get => GetProperty(PropertyInt.Value);
    set { if (!value.HasValue) RemoveProperty(PropertyInt.Value); else SetProperty(PropertyInt.Value, value.Value); }
}
```

Plain public get/set, same declaring file as `TimeToRot` (`CorpseWatch`) and `PlayerKillerStatus` (`PKLiteZone`).

Inventory/equipment enumeration was also re-verified against fresh full-file fetches this round:
- `Container.cs`: `public Dictionary<ObjectGuid, WorldObject> Inventory { get; }` - main pack + side-pack items, on `Container` (base of `Player`).
- `Creature_Equipment.cs`: `public Dictionary<ObjectGuid, WorldObject> EquippedObjects { get; }` - everything currently worn/wielded, on `Creature` (base of `Player`).

Both public, both already read directly by other shipped mods in this repo (`BurdenCheck`/idea 58 reads
`Inventory`; `Creature_Magic.cs` itself reads `EquippedObjects.Values`).

## Risks

- Stack-total-vs-per-unit ambiguity on `Value` for stackables was not independently re-verified this round
  (see above) - this mod's explicit `* StackSize` is a documented assumption, not a confirmed fact, and is
  called out again here per the idea's own risk note.
- A `null` `Value` is shown as excluded (with a count), never silently folded into the total as zero.
- Purely additive and read-only: no interaction with combat, trading, or the database.

## How to test

1. Build: `bash mods-proposed/check-mod.sh NetWorthCheck` (must print `MOD OK`).
2. To try in game later (Tom's call - this mod ships `Enabled: false`): flip `Meta.json` to `Enabled: true`,
   place the built folder in the server's `Mods` directory, run `mod find`, log in, and run `/networth` with a
   mix of stacked and unstacked items in your pack and worn slots. Confirm the pack/worn/combined numbers add
   up, and that any zero-`Value` weenie shows up in the "excluded" count instead of silently adding 0.

## How to enable later

Same pattern as every other proposed mod here: set `Meta.json`'s `"Enabled"` to `true`, ship the built folder
into the server's `Mods` directory (Tom's call, not automatic - this repo never deploys from `mods-proposed/`).
