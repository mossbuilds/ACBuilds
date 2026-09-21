# DeathReport (proposed, not deployed)

Postfix on `Player.OnDeath`: records death position (in memory), appends `time | name | LOC` to `deaths.log`, and broadcasts a random fun line to all players (global cooldown). Player command `/lastdeath` teleports back to the last death spot (per-player cooldown).

Settings.json: `Enabled`, `Broadcast`, `BroadcastCooldownSeconds`, `AllowLastDeath`, `LastDeathCooldownSeconds`, `LogFile`, `Messages` ({0} = name).

Verified against ACE source: `Player.OnDeath(DamageHistoryInfo, DamageType, bool)` (ACE.Server.WorldObjects, DamageHistoryInfo in ACE.Server.Entity), `Player.Teleport(Position, bool)`, `Position` (ACE.Entity, copy ctor, ToLOCString), `PlayerManager.BroadcastToAll(GameMessage)` (ACE.Server.Managers). No built-in `lastdeath` command.

Risks: broadcast spam (cooldown); /lastdeath lets players return to a dangerous spot; positions lost on restart. Test: die, check chat and deaths.log, run /lastdeath. Enable later by copying to mods/.
