# ZoneSweep

A scoped, confirm-gated alternative to ACE's only bulk player action, `PlayerManager.BootAllPlayers()` (server-wide,
unconditional, no preview - meant for a full world shutdown). ZoneSweep clears **one landblock** instead, for
maintenance or a private event. **Disabled by default** (`Enabled=false`).

## Command
`/zonesweep <landblock hex> [confirm]` (AccessLevel.Sentinel+)

1. **Preview** - `/zonesweep 0198` lists the online characters currently in that landblock (via
   `PlayerManager.GetAllOnline()` filtered by `Player.Location.Landblock`) and starts a `ConfirmSeconds` window.
2. **Confirm** - `/zonesweep 0198 confirm` inside that window, with the same set of players still there, warns each
   affected player by chat, waits `WarnSeconds`, then logs each one off with their own `Player.LogOut()` - ACE's
   normal, graceful per-player logout path (same one a client-side logout takes). No `Session.Terminate`, no
   `BootAllPlayers`, no character/item edit.
3. A stale confirm is rejected and re-previewed instead of acting: expired window, wrong landblock, or the roster in
   that landblock changed since the preview (someone left or arrived) all start a fresh preview.
4. Refuses with "nothing to do" if the landblock has zero online players, at preview or confirm time.

## Scope guarantees
- Only players whose current `Location.Landblock` equals the named landblock are ever warned or logged off.
- Nothing server-wide is ever broadcast; only the affected players see the warning chat line.
- No creature, object, or database row outside a normal player logout is touched. Never touches `ace_auth` or
  `ace_shard*` directly - it calls the same in-process ACE API a player's own logout button calls.

## Access level
`AccessLevel.Sentinel` - the same bar this mod set already uses for TimedMute (a staff action that changes another
player's state without their consent). Not raised to `Admin`: the mandatory preview, short confirm window, and
single-landblock scope already bound the blast radius, the same way ACE trusts Sentinel-tier staff with other
other-player-affecting moderation commands. Not lowered to `Player`: this force-disconnects other people's
characters and needs a real staff decision, unlike a self-service command such as SkillRespec.

## Settings
- `Enabled` (false)
- `ConfirmSeconds` (60) - how long a preview stays valid for a follow-up `confirm`
- `WarnSeconds` (8) - delay between the in-chat warning and the actual logoff, so players see it coming
- `WarnMessage` - `{0}` = WarnSeconds
- `LogFile` (`zonesweep.log`), `MaxKb` (512), `MaxFiles` (5) - rotated log, same scheme as TradeLedger. Logs who ran
  it, the landblock, how many players were affected, and a UTC timestamp - never account names or IPs.

## Risks / notes
- A player who changes zones during the `WarnSeconds` delay is re-checked right before logoff and skipped if they
  are no longer in the target landblock.
- Landblock is the 4-digit hex ACE itself prints for a location (accepts `0198`, `198`, or `0x198`).
- Test: enable on a test server, walk a second character into a landblock, run `/zonesweep <lb>` (preview), then
  `/zonesweep <lb> confirm` and confirm only that character is warned and logged off.
- Enable later: set `Enabled` true in Settings.json.
