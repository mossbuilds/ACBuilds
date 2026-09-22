# TickLagAlert

Push counterpart to `ServerPulse`/`HotspotAlert` (which report landblock/player load), but for the world tick's own duration. ACE already times the whole game loop internally via `ServerPerformanceMonitor` (`ACE.Server.Managers`), but that monitor only ever reports on manual pull (`@monitor performance` / its `ToString()`). This mod polls it on a throttled timer and whispers online Sentinel+ staff once the tick is sustained-slow, instead of requiring an admin to think to ask. Never modifies server behavior, never touches the databases, writes no files. Off by default.

## Command
`/lagwatch [on|off|status]` (Sentinel): toggles the poll (in memory only - edit Settings.json to persist across restarts) or shows the current last-tick duration and consecutive-slow-check count on demand.

## Settings (Settings.json)
`Enabled` (false), `WarnSeconds` (0.5) - `UpdateGameWorld_Entire`'s `LastEvent` above this counts as one slow check, `SustainedTicks` (3) - consecutive slow checks required before alerting, to avoid tripping on a single GC pause, `PollSeconds` (10, minimum 5), `AlertCooldownMinutes` (10).

## Verified against ACE master (raw source, full-file fetch)
- `Source/ACE.Server/Managers/ServerPerformanceMonitor.cs`: `public static class ServerPerformanceMonitor` with `public static bool IsRunning`, `public static TimedEventHistory GetEventHistory5m(MonitorType monitorType)` (also `1h`/`24h` variants, not used here), and `public enum MonitorType` including `UpdateGameWorld_Entire`. The class's own `ToString()` labels `UpdateGameWorld_Entire` as "WorldManager.UpdateGameWorld() time not including throttled returns" - confirming it times the whole tick, not a sub-phase.
- `Source/ACE.Common/Performance/TimedEventHistory.cs`: `public class TimedEventHistory` with `public double LastEvent { get; private set; }` (and `TotalEvents`, `TotalSeconds`, `LongestEvent`, `ShortestEvent`, `AverageEventDuration`, all public, none used here). All public, no reflection needed.
- `PlayerManager.GetAllOnline()`, `Player.Session`, `Session.AccessLevel` (ACE.Server.Network) and `ActionChain(IActor, Action)` (ACE.Server.Entity.Actions) - same pattern as HotspotAlert.
- Command `lagwatch` is not one of the 327 stock console commands (checked against the built-ins list).
- Source: https://raw.githubusercontent.com/ACEmulator/ACE/master/Source/ACE.Server/Managers/ServerPerformanceMonitor.cs and .../Source/ACE.Common/Performance/TimedEventHistory.cs (both fetched in full this round).

## Risks
`ServerPerformanceMonitor.IsRunning` must already be true (started via the stock `@monitor performance start` path) or `GetEventHistory5m` reads all-zero history; `/lagwatch status` and the poll both detect this and say so explicitly rather than reporting a false "all clear". Polling itself is a couple of property reads on a throttled timer and does not run inside the tick it measures. `WarnSeconds`/`SustainedTicks` need tuning per server hardware - a value tuned for one box will false-alarm or stay silent on another.

## Test
Start the stock monitor (`@monitor performance start`), set `Enabled=true` and a low `WarnSeconds`/`SustainedTicks` in Settings.json, log in as Sentinel+, wait one poll interval or run `/lagwatch status`. Enable later by copying the built folder into mods/.
