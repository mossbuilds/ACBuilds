# ContractTracker (proposed, not deployed)

`/mycontracts`, read-only, off by default (idea 60 in `IDEAS.md`).

## Why
ACE ships a Sentinel-only `@contract` command that can inspect any player's contract state. There is no
equivalent a normal player can run on themselves. This adds the self-service version: a player lists
their own tracked contracts, current stage, and time remaining, with no admin access required.

## What it shows
`/mycontracts` reads the caller's own `Player.ContractManager.ContractTrackerTable` (a live dictionary,
recomputed from the character's saved contract registry on every read - not cached, so it never goes
stale) and for each entry prints:
- `Contract.ContractName` (falls back to the raw contract ID in hex if the DAT lookup for that ID fails)
- A stage description built from `ContractTracker.Stage` (see below)
- `TimeWhenDone` / `TimeWhenRepeats`, formatted as a human duration, when the stage makes them relevant

## Every named member was re-verified against the live ACE source (master), not inferred
- `Player.ContractManager` - public field, `ACE.Server.WorldObjects/Player.cs`
- `ContractManager.ContractTrackerTable` - public get-only property, `ACE.Server.WorldObjects.Managers/ContractManager.cs`
- `ContractTracker.Contract` - public get-only property, `ACE.Server.Network.Structure/ContractTracker.cs`
- `ContractTracker.Stage` - public field, type `ACE.Server.Network.Structure.ContractStage`
- `ContractTracker.TimeWhenDone` / `TimeWhenRepeats` - public fields, `double`
- `Contract.ContractId` / `Contract.ContractName` - public get (private set), `ACE.DatLoader.Entity/Contract.cs`

Nothing here needed a reflection fallback - unlike some earlier mods in this repo (e.g. `StuckVendorWatch`,
`VendorStock`), every field this idea needed turned out to already be public.

## What diverged from the idea text
The idea's own risk note said the `ContractStage` enum's "full value set was only seen partially" and was
right to flag that. The real enum (`ACE.Server.Network.Structure/ContractTracker.cs`) has **four** named
values, not the two the idea quoted:
```
Available = 0x1, InProgress = 0x2, DoneOrPendingRepeat = 0x3, ProgressCounter = 0x4
```
More importantly, `ContractTracker`'s own `CheckAndSetStage()` does
`Stage = ContractStage.ProgressCounter + progress;` for any contract using a progress-counter quest flag -
so the runtime value can be `ProgressCounter + N` for any `N`, a value never named in the enum at all. A
plain `switch` over the named values would misreport or throw on every progress-tracked contract past the
first step. `HandleMyContracts` therefore treats anything `>= ProgressCounter` as "in progress, N steps
completed" (deriving N from the enum's own encoding), and anything else unrecognized falls through to a
generic `unrecognized stage (<int value>)` message rather than assuming only the two values the idea text
happened to quote.

`TimeWhenDone`/`TimeWhenRepeats` are also **not** absolute timestamps despite their names suggesting one -
`ContractTracker`'s constructor sets them from `QuestManager.GetNextSolveTime(...).TotalSeconds`, which is
a countdown (seconds remaining), confirmed by reading that constructor body directly rather than guessing
from the field names.

## Everything else
Read-only, no `ace_auth`/`ace_shard` access beyond the normal character load ACE already does, no Harmony
patches at all (pure command handler + property reads), no world state changed. Off by default for
consistency with every other mod here. Command name `mycontracts` re-confirmed free against a fresh
`%TEMP%\cmds.txt` dump (327 built-ins, zero hits) and does not collide with the built-in `@contract`.
