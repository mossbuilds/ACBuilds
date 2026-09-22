# FellowshipPulse

Player-facing fellowship overview. ACE's only fellowship introspection tools are the sentinel/admin-only
debug commands `@fellow-dist` and `@fellow-info`; an ordinary fellowship member has no in-game way to see
who is in XP-share range. This adds the read-only, own-fellowship-only equivalent for players.

## Command

`/fellow` (no arguments). Off by default. ("fellow" is not one of ACE's built-in commands, checked
against the built-in list in `%TEMP%\cmds.txt`; it does not clash with the built-in `fellow-info`,
`fellow-dist` or `fellowbuff` commands.)

Prints, in chat, only to the caller:
- Fellowship name and leader.
- Member count (fellowships only track online members - ACE drops a member from its own roster the
  moment they log off, so "members online" is the whole roster).
- Whether XP sharing is on, and whether it is an even split or weighted by level (`Fellowship.EvenShare`).
- Each member: level, HP/stamina/mana as percents, and whether they are within XP-share range of the
  caller (`Fellowship.WithinRange`).

## Data read

Everything comes from the caller's own `Player.Fellowship` (an `ACE.Server.Entity.Fellowship`):
`FellowshipName`, `FellowshipLeaderGuid`, `GetFellowshipMembers()` (online members only, `Dictionary<uint,
Player>`), `WithinRange(Player, bool includeSelf)` (radar-range/same-landblock check, `List<Player>`),
`ShareXP` and `EvenShare` (bool fields). Per-member vitals use `Player.Health`/`Stamina`/`Mana`
(`ACE.Server.WorldObjects.Entity.CreatureVital`), read via its own `Percent` property (same vitals
objects StatCard reads `MaxValue` from).

Never reads any other fellowship, any account data, or any offline member's data beyond what
`GetFellowshipMembers()`/`WithinRange` already expose for online players. No files, no world objects,
no writes anywhere - purely a formatted read of in-memory server state, sent only to the caller.

## Not done

No admin/other-fellowship variant (out of scope by design - the idea is "the caller's own fellowship,
at a glance"). No distance/unit display (`WithinRange`'s threshold is ACE's own radar range / landblock
rule and isn't exposed as a number to print). Offline fellowship members are never listed, because ACE's
`Fellowship.FellowshipMembers` itself does not keep them once they log off - there is nothing offline to
show, so "who is online" is implicit (everyone shown is online) and only in-range/out-of-range varies.

## Risks

`WithinRange`'s exact distance rule is ACE's own and not configurable here. `CreatureVital.Percent` and
`Fellowship`'s public members could change signature in a future ACE version.
