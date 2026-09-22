# EventClock

Runs ACE's own named world events on a schedule. **Off by default** (`Enabled=false`).

## Approach and why
ACE already has a full event on/off system driven from the ace_world `events` table - `EventManager.StartEvent(string, WorldObject, WorldObject)`, `StopEvent(...)`, `IsEventStarted(...)`, `IsEventAvailable(string)`, `GetEventStatus(string)` returning `GameEventState` (all in `ACE.Server.Managers`, verified against raw source) - but it is exposed only through the manual Sentinel commands `@event_start`/`@event_stop`/`@event_list`, so a timed event only turns on if a staff member is online and remembers at the right moment. EventClock reads an owner-edited list of `{EventName, Days, Start, End}` windows and calls `StartEvent`/`StopEvent` for names that already pass `IsEventAvailable` - it creates no new events, only toggles ones the operator already defined in the database. `source`/`target` are passed as `null` (verified: both are optional, used only for log-line context inside `StartEvent`/`StopEvent`).

No world object, no file, no account name or IP, and no direct read/write of `ace_auth`/`ace_shard` is touched by this mod.

## Commands (Admin)
`/eventclock [status]` - shows Enabled, the check interval, and each scheduled event's window and current `GameEventState` (via `GetEventStatus`). It does not start or stop events itself outside the schedule - use ACE's own `/event` command for a manual override.

## Settings (Settings.json)
- `Enabled` (false)
- `CheckSeconds` (30) - schedule check interval; a lower value is clamped up to 30
- `Events`: list of `{ EventName, Days: ["Friday", ...] (empty = every day), Start: "HH:mm", End: "HH:mm" (may cross midnight) }`

## Schedule check
A `System.Threading.Timer` fires every `CheckSeconds` (>= 30s). For each configured event it re-evaluates whether "now" (server local time) falls in the event's Days/Start/End window - same same-day/crosses-midnight logic as PkNight's `InWindow` - and, if the window state differs from what EventClock itself last set, calls `StartEvent` or `StopEvent` and broadcasts it. EventClock tracks "did I turn this on" itself (`EventManager` exposes no bulk on/off list), so a manual `@event_start`/`@event_stop` by staff on a scheduled event's name will be overwritten on the next tick.

## Risks / test
A schedule boundary missed while the server is down is **not** caught up - the mod does not fire a late start or a late stop, it just applies whatever the clock says on the next tick after startup. A typo'd `EventName` is logged and skipped (`IsEventAvailable` returns false), not raised as an in-game error. Not compiled by the author; run `check-mod.sh EventClock`.

## Enable later
Copy to the mods folder, list real event names from `ace_world.events` in `Settings.json`, set `Enabled` true, reload.
