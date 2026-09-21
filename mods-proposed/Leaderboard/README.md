# Leaderboard (proposed, not deployed)

Player command `/top`: top levels (all characters, cached) and top monster killers (tally kept by this mod).

Settings.json: `Enabled`, `Size` (max 25), `CacheSeconds`, `DataFile` (kill tally JSON, saved every 5 min and on stop; no database access).

Verified against ACE source: `PlayerManager.GetAllPlayers()` returns `List<IPlayer>`, `GetOnlinePlayer(uint)`, `GetOfflinePlayer(uint)` (ACE.Server.Managers); `IPlayer` (ACE.Server.Entity) has `Name`, `Level`, `Guid`; `Creature.OnDeath(DamageHistoryInfo, DamageType, bool)` (ACE.Server.WorldObjects), `DamageHistoryInfo.TryGetAttacker()` (ACE.Server.Entity). IPlayer has no total XP, so ranking is by level only. `top` is not a built-in command.

Risks: the kill tally counts monster kills only (player victims skipped), credits the last damager; a crash loses up to 5 min of tally. Test: kill something, `/top`. Enable later by copying to mods/.
