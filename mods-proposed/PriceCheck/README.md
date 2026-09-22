# PriceCheck (proposed, not deployed, not compiled)

Read-only player command that tells the caller what an item is worth. Never touches other players'
items, never writes files, never spawns/modifies world objects. Off by default (`Enabled: false` in
Meta.json and in Settings.json's own `Enabled`).

## Command
`/pricecheck [item name]` (any player):
- No args: uses the caller's **last appraised object** (whatever they last examined - may be any
  object they can currently resolve, matching ACE's own `/appraise`-adjacent dev commands).
- With an item name: searches the caller's **own inventory only** (pack, side/sub-containers, and
  worn/equipped items) - never other players'.

Reports: item name, base `Value`, an estimated vendor sell price (with the multiplier used, stated
in the reply), stack size (if >1), and Attuned/Bonded/Retained flags with a note that a flagged item
won't sell to a vendor.

Free command names checked against `%TEMP%\cmds.txt` (no ACE built-in clash): `pricecheck`, `worth`,
`appraiseprice`. Registered only `pricecheck`; the other two are free if Tom wants an alias later.

## How the sell price is computed
`Vendor.GetBuyCost(WorldObject item)` is what a vendor actually pays a player selling an item to it
(`ACE.Server.WorldObjects.Vendor`, `Vendor.cs`, verified on `master`):

```csharp
public int GetBuyCost(WorldObject item) => GetBuyCost(item.Value, item.ItemType);
private int GetBuyCost(int? value, ItemType? itemType)
{
    var buyRate = BuyPrice ?? 1;                     // Vendor's own PropertyFloat.BuyPrice, default 1.0
    if (itemType == ItemType.PromissoryNote) buyRate = 1.0;
    var cost = Math.Max(1, (int)Math.Floor(((float)buyRate * (value ?? 0)) + 0.1));
    return cost;
}
```

(`GetSellCost`, by contrast, is what a **player pays** a vendor to buy something - `PropertyFloat.SellPrice`,
default 1.0 - the wrong direction for this mod and not used.)

PriceCheck has no specific vendor to ask (the command targets an item, not a vendor), so it
reproduces this exact formula with a **configured** `AssumedBuyPriceMultiplier` (default `1.0`, the
same "no markdown" fallback `BuyPrice ?? 1` uses) standing in for a real vendor's `BuyPrice`
property. The reply always states the multiplier and that real vendors vary. Promissory notes still
get the 1.0x override, matching ACE's own vendor code.

Not used/considered: `vendor_unique_rot_time` (a server property, unrelated - governs how long a
vendor's rotating unique stock persists, not price). `Vendor.AlternateCurrency`
(`PropertyDataId.AlternateCurrency`, verified in `Vendor.cs`) exists but only applies to a vendor
that trades in something other than pyreals; since this mod never surveys a specific vendor it
always estimates in pyreals and says so, rather than guessing which vendor's currency would apply.

## Flags
`WorldObject.Attuned` (`AttunedStatus?`, `PropertyInt.Attuned`), `WorldObject.Bonded`
(`BondedStatus?`, `PropertyInt.Bonded`), `WorldObject.Retained` (`bool`, `PropertyBool.Retained`) -
all verified public properties in `WorldObject_Properties.cs` (`ACE.Server.WorldObjects`). Any of
Attuned (Attuned/Sticky), Bonded (Bonded/Sticky/Destroy) or Retained is reported as "won't sell to a
vendor" per the brief. Verified in `Player_Commerce.cs` (the actual sell-to-vendor path): it only
checks `wo.Retained` and the separate `wo.IsSellable` (`PropertyBool.IsSellable`, default true) -
Attuned/Bonded are **not** independently checked there in vanilla ACE. So Retained is confirmed to
block a real sale; Attuned/Bonded are reported here as informational flags per the brief, not as a
verified sale-blocker in ACE's own code.

## Getting the last-appraised item
`CommandHandlerHelper.GetLastAppraisedObject(Session)` (`ACE.Server.Command.Handlers`) is what every
admin command in `DeveloperCommands.cs` calls for this - but the class itself is declared
`internal static class` in `CommandHandlerHelper.cs`, so it is **not visible** from a separate mod
assembly (verified by reading the file; confirms the idea's "internal-ish" flag as INTERNAL, not
merely low-visibility). PriceCheck instead inlines that method's own three lines: read
`Player.RequestedAppraisalTarget` (`public uint?`, `Player_Properties.cs`, verified - this is the
field `GetLastAppraisedObject` itself reads, not `CurrentAppraisalTarget`, which a couple of other
dev commands use for a different purpose/HUD target) and resolve it with
`Player.FindObject(uint, Player.SearchLocations, out, out, out)` (public, `Player_Inventory.cs`,
verified) using `SearchLocations.Everywhere`.

## Own-inventory search
No ACE method matches inventory items by display name; `Container.GetInventoryItemsOfTypeWeenieType`,
`GetInventoryItemsOfWCID` and `GetInventoryItemsOfWeenieClass` (all public, `Container.cs`, verified)
match by weenie type/WCID/class name, not the player-visible name. PriceCheck walks
`Container.Inventory` (`public Dictionary<ObjectGuid, WorldObject>`, verified) recursively into side
containers the same way `GetInventoryItemsOfWeenieClass` does, plus `Creature.EquippedObjects`
(`public Dictionary<ObjectGuid, WorldObject>`, verified `Creature_Equipment.cs`), matching
`WorldObject.Name` case-insensitively (exact match preferred, first substring match as fallback).

## Settings
`Enabled` (false), `AssumedBuyPriceMultiplier` (1.0).

## Unverified / assumptions
- Whether any live vendor on this shard actually sets `BuyPrice` below 1.0 (typical for a "cheap
  buyer" vendor) is not checked - the reply's "real vendors vary" line exists because of this.
- Whether ACE's actual sell-to-vendor path (`Player_Commerce.cs`) has additional, currently
  unread checks beyond Attuned/Bonded/Retained that block a sale (e.g. quest-restricted items) -
  not verified; the flags line only reports the three properties named in the brief.
- Not compiled or run against a live server.
