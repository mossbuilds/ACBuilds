# WorldBoss

Admin- or schedule-triggered world boss. **Disabled by default** (`Enabled=false`, `ScheduleEnabled=false`).

- `/worldboss start [wcid]`, `/worldboss stop`, `/worldboss status` (Admin). Default boss wcid 900021232 (Old Whiskers).
- Exactly one boss object at a time; auto-despawn after `MaxMinutes` with an announcement.
- Spawn at `Cell`/`X`/`Y`/`Z`, or next to the admin if `SpawnNextToAdmin`. Scheduled starts always use the configured position.
- Death broadcast names the top damage dealer (postfix `Creature.Die`); optional `RewardPyreals` coin stack (wcid 273) to that player.
- No DB access, no files written. Test: enable, set position, `/worldboss start`.
