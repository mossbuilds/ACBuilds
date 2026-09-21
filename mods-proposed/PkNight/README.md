# PkNight

Scheduled or admin-started PK windows on an otherwise PvE server. **Off by default** (`Enabled=false`).

## Approach and why
Damage rules, not status changes. In ACE, `Player.SetPlayerKillerStatus` only ever sets NPK or Free (PK and PKLite are coerced to NPK), and `PlayerManager.UpdatePKStatusForAllPlayers` rewrites every offline character's stored status. Neither is safely reversible for an event. Instead a Harmony postfix on `Player.CheckPKStatusVsTarget` clears the "not PK" / "not same PK type" refusals for player-vs-player while the window is active. No character property is changed or saved; when the window ends (or the server restarts or the mod stops) normal rules return at once. House-boundary refusals are kept. Databases are never touched.

## Commands (Admin)
`/pknight start|stop|auto|status` - start now, stop now, return to schedule, or show state.

## Settings (Settings.json)
Enabled (false), Days (["Friday"]), Start ("20:00"), End ("22:00", may cross midnight; server local time), WarnMinutes (10), SafeLandblocks (hex list that stays safe).

## Announcements
All players get a world broadcast when a window is about to start, begins, and ends.

## Risks / test
The client may still gate attacks on NPK targets; test with two characters in game. Death penalties follow ACE's normal PvP-kill rules (not verified). Use SafeLandblocks for tutor zones (or pair with PkGuard). Not compiled by the author; run `check-mod.sh PkNight`.

## Enable later
Copy to the mods folder, set `Enabled` true in Settings.json, reload.
