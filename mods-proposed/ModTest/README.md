# ModTest

Admin `/modtest` (RequiresWorld); `modtest` is not in the 327 built-ins. Off by default (Enabled=false in Settings.json and Meta.json). Read-only: no spawns, teleports, casts, files, DB writes.

## Checks
1. Mods: `ModManager.GetModContainerByName(name, false)` for each ExpectedMods name; PASS if `Status==Active`, FAIL if present but not active, SKIP if not found. (ModManager.Mods is private, so not used.)
2. Commands: `CommandManager.GetCommands()` contains each ExpectedCommands name, only for mods that are active (else SKIP). MinionOrders registers `order` (checked: not an ACE built-in), so it is in the list.
3. Weenies: `DatabaseManager.World.GetCachedWeenie(wcid) != null` for RequiredWeenies.
4. Properties: `PropertyManager.GetBool(key, false, false)` does not throw (value never printed).
Per-mod Settings.json Enabled is not readable from here; "active" is the reported state.

## Unverified
Not compiled. GetModContainerByName body not read past its signature (assumed exact-name match when allowPartial=false); the default property key world_closed was not confirmed (unknown keys just return the fallback); GetCachedWeenie from a command thread assumed safe (ConcurrentDictionary cache).
