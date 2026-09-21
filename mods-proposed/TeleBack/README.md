# TeleBack (proposed, not deployed)
Admin-only undo for teleports. Harmony prefix on `Player.Teleport(Position, bool)` records a copy of the current Location for Session.AccessLevel >= MinAccess (Sentinel); `/teleback [n]` teleports back and pops n entries.
Ping-pong: `/teleback` adds the guid to an `undoing` set before `WorldManager.ThreadSafeTeleport`; the prefix removes it and skips recording.
Settings: Enabled (false), MinAccess (Sentinel), HistorySize (5). Memory only, cap 200 admins; no files/DB.
Unverified: not compiled; a failed teleport that never reaches Teleport would leave a stale undoing mark; logout not cleared.
