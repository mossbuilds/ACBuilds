# AnnounceEvents (proposed, not deployed)
Scheduled broadcasts plus admin event announcements. Verified in ACE source: `PlayerManager.BroadcastToAll(GameMessage)`, `GameMessageSystemChat`.
Commands (Admin): `/announce start <name>`, `/announce stop`, `/announce say <text>`.
Settings.json: `IntervalSeconds` (900), `Messages` (list, cycled in order; empty disables).
Announcement only: it does not change XP or loot rates (pair with XpBoost). Risks: timer runs on a thread pool thread; BroadcastToAll only enqueues network sends. Settings are read at world open (restart to change).
Test: set IntervalSeconds to 10, log in, wait; run each /announce form. Enable later: copy folder to the mods dir.
