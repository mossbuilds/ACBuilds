# HotspotAlert

Crowd guard for admins. A throttled timer scans loaded landblocks and whispers online admins (Sentinel and up) when one holds too many creatures or players (the usual cause of lag and headset memory pressure). Once per cooldown per landblock. Never spawns, deletes or changes anything, never touches the databases, writes no files. Off by default.

## Command
`/hotspots` (Sentinel): lists landblocks over the thresholds right now. Shown as the 4-hex-digit block id only.

## Settings (Settings.json)
`Enabled` (false), `CreatureThreshold` (150), `PlayerThreshold` (40), `ScanSeconds` (60, minimum 30), `AlertCooldownMinutes` (10).

## Verified against ACE master
`LandblockManager.GetLoadedLandblocks()` (ACE.Server.Managers), `Landblock.GetAllWorldObjectsForDiagnostics()` / `Landblock.Id` (ACE.Server.Entity), `PlayerManager.GetAllOnline()`, `Player.Session`, `Session.AccessLevel` (ACE.Server.Network), `ActionChain(IActor, Action)` (ACE.Server.Entity.Actions). Command `hotspots` is not a stock name.

## Risks
Scan allocates a list per loaded landblock every ScanSeconds; keep 30+. Events will trip the thresholds on purpose; raise them or disable during events. Settings are read once at world open.

## Test
Set Enabled=true and low thresholds, log in as an admin, wait one scan or run `/hotspots`. Enable later by copying the built folder into mods/.
