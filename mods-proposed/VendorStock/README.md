# VendorStock (proposed, not deployed)

`/vendorstock`, read-only, off by default (idea 52 in `IDEAS.md`).

## Why
The ACE wiki's [Server-Configurable-Options](https://github.com/ACEmulator/ACE/wiki/Server-Configurable-Options)
page documents `vendor_shop_uses_generator` ("enables or disables vendors using generator system in addition to
createlist to create artificial scarcity") - but nothing in stock ACE tells a *player* whether a given vendor is
one of these, or why its stock might behave unpredictably. This closes that visibility gap.

## What it shows
Reads the caller's **last appraised object** (examine a vendor, then run the command - same lookup pattern
`PriceCheck` already uses, since `CommandHandlerHelper` is `internal` to `ACE.Server` and not reachable from a mod).
If it's a `Vendor`, prints:
- `OpenForBusiness` (public bool property) - is it currently functioning at all.
- `DefaultItemsForSale.Count` / `UniqueItemsForSale.Count` (both public `Dictionary<ObjectGuid, WorldObject>`) -
  how many built-in vs. player-sold items it currently has.
- `IsGenerator` (public bool on `WorldObject`, `WorldObject_Generators.cs`) - whether this vendor also restocks via
  the generator system, the exact ambiguity the wiki page flags.

## What it deliberately does NOT show, and why
The original idea also wanted "seconds until next reset," from `Vendor.ResetTimestamp`/`ResetInterval`. **A real
compile attempt** (not just a source read) showed `ResetTimestamp` is `protected`, not public - it's a real member
of `Vendor`/`WorldObject` (confirmed by `Vendor.cs` itself using it as a bare identifier internally), but a mod
cannot read it without Harmony reflection. That reflection risk wasn't judged worth it for a "nice to have"
countdown number on a purely cosmetic QoL command, so this mod says so plainly in its own output rather than
silently omitting the information or reflecting into a protected field to get it.

## Everything else
Read-only, no `ace_auth`/`ace_shard` access, no world state changed, no Harmony patches at all (pure command
handler + property reads). Off by default for consistency with every other mod here, even though nothing it does
requires that gate.

## Process note
This mod exists as `VendorStock`, not `VendorStockPeek`. An earlier build attempt under that name paraphrased idea
52 incorrectly (it built a "browse the nearest vendor's stock and prices" command instead of this one) and was
discarded before compiling rather than shipped as a mismatch with the sourced idea.
