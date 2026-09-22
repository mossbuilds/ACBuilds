# BurdenCheck

Read-only `/burden` (all players): current carry weight (`EncumbranceVal`), capacity (`GetEncumbranceCapacity()`), the hard cap (`capacity * 3`, the same multiplier ACE's own inventory-add checks use), and available headroom (`GetAvailableBurden()`). Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure property/method reads.

## Verified against ACE master (Source/ACE.Server/WorldObjects/Player_Inventory.cs), re-fetched for this build
- `GetEncumbranceCapacity()` - public `int`, body `(int)((150 * strength) + (AugmentationIncreasedCarryingCapacity * 30 * strength))`. Confirmed public, body matches the idea entry exactly.
- `GetAvailableBurden()` - public `int`, body `(GetEncumbranceCapacity() * 3) - EncumbranceVal ?? 0`. Confirmed public, body matches the idea entry exactly.
- `EncumbranceVal` - nullable `int` property (`PropertyInt.EncumbranceVal`), read defensively with `?? 0` the same way the rest of `Player_Inventory.cs` reads it (e.g. the property-update message sends `EncumbranceVal ?? 0`).
- The hard cap (`capacity * 3`) is the same multiplier baked into `GetAvailableBurden()`'s own body and used by ACE's own inventory-add capacity checks in the same file.

## Not re-verified / out of scope by design
- `AugmentationIncreasedCarryingCapacity` - only ever seen as a bare identifier inside `GetEncumbranceCapacity()`'s own body, never in a declaration line or an external access chain. Accessibility unverified; may need Harmony `Traverse` or may not be readable at all. This command never reads it directly - `GetEncumbranceCapacity()` already bakes its effect into the capacity number displayed, so the risk affects nothing this command needs.

## Unverified
- Not compiled (per instructions) - a real build against the ACE binaries is the final check.
- Command name `burden` checked against `%TEMP%\cmds.txt`: not a built-in (`raise`/`unfreeze`/`resyncproperties` are; `order`/`myguests`/`myhooks`/`genaudit` are not, and neither is `burden`).

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/burden`.
