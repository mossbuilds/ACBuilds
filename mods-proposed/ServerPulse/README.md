# ServerPulse

Read-only health snapshot for admins (ACE issue #718 idea). Never changes anything, never reads the databases.

## Command
`/pulse [top]` (Sentinel and up): online count, loaded landblocks (dormant count), total creatures, and the busiest `top` landblocks by players then creatures. Landblocks shown as the 4-hex-digit block id (e.g. `A9B4`); no accounts, IPs or paths.

## Settings (Settings.json)
- `TopN` (5): default list length. `MaxTopN` (20): cap on the argument.

## Verified against ACE master
`LandblockManager.GetLoadedLandblocks()` (snapshot list), `Landblock.GetAllWorldObjectsForDiagnostics()` (snapshot list, documented for cross-thread use), `Landblock.Id` / `IsDormant`, `LandblockId.Raw`, `PlayerManager.GetOnlineCount()`. Command name `pulse` is not a stock command.

## Risks
Walking every object of every loaded landblock allocates a list per block; fine on demand, do not script it in a tight loop.

## Test
Log in as an admin, run `/pulse` and `/pulse 10`. Enable later by copying the built folder into mods/.
