# FellowshipRejoinWindow

Idea 84 (Round 14). Player command `/fellowlock` answers "if I leave this fellowship right now, can I get back in" - the only in-game signal that a fellowship lock exists is a single broadcast line sent once, at the moment of locking (`Fellowship.UpdateLock`), with no way to check the state afterward.

## What it does

- `/fellowlock` (`AccessLevel.Player`, in-world only): reports on the caller's own fellowship.
  - Not in a fellowship: says so.
  - Fellowship not locked: says so.
  - Fellowship locked, caller currently a member: says it is locked but does not affect them.
  - Fellowship locked, caller left it and is inside the 600-second rejoin grace window: reports seconds remaining.
  - Fellowship locked, caller left it and the window has expired: says so.

## What it doesn't do

- No admin/target-player variant - only ever reads the caller's own fellowship.
- No patches, no Harmony hooks on any fellowship method - pure read of existing state.
- Cannot lock, unlock, or otherwise change a fellowship. Never calls `AddFellowshipMember` or `UpdateLock`.
- Adds no world objects, writes no files.

## Hooks used (all verified public, `ACE.Server.Entity.Fellowship`, `Source/ACE.Server/Entity/Fellowship.cs`, fetched in full this round from ACEmulator/ACE master)

- `Player.Fellowship` (`ACE.Server.WorldObjects`) - null if not in a fellowship. Same access pattern as the already-shipped `FellowshipPulse`.
- `Fellowship.IsLocked` (`public bool`) - set only via emote (`UpdateLock`), never by a player action.
- `Fellowship.DepartedMembers` (`public Dictionary<uint, int>`) - guid to Unix timestamp of departure, recorded by `QuitFellowship` only while `IsLocked` is true, and cleared each time `UpdateLock` re-locks the fellowship.
- The 600-second window is computed exactly as `AddFellowshipMember` computes it: `Time.GetDateTimeFromTimestamp(timeDeparted).AddSeconds(600)`, compared against `DateTime.UtcNow`. This mod reuses that same math (`ACE.Common.Time`) so it reports exactly what a recruit attempt would see.

Not independently re-verified this round: `FellowshipLockData`'s own member names (only its dictionary key/value shape via `FellowshipLocks` was confirmed) - this command does not read `FellowshipLocks` or need those members, so it does not block the idea, but is flagged per this repo's citation practice.

## Settings

`Settings.json` - `Enabled` (bool, default `false`). No other settings; this is a pure read with nothing to tune.

## Risks

None identified. Read-only, no patches, no state mutation, no target other than the caller's own fellowship.

## How to test

1. Two test characters. Have one form a fellowship and invite the other.
2. Have an admin lock the fellowship via the emote path (or wait for content that locks one).
3. `/fellowlock` as a member still in the locked fellowship - expect "locked... does not affect you."
4. Have the second member leave, then `/fellowlock` as that player - expect a countdown from ~600 seconds.
5. Wait past 600 seconds (or adjust the test window) and run `/fellowlock` again - expect "window has expired."
6. `/fellowlock` while not in any fellowship - expect "You are not in a fellowship."

## How to enable later

Set `Enabled: true` in this mod's `Settings.json` and reload/restart. Off by default like every other proposed mod here.
