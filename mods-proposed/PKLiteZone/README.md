# PKLiteZone

Opt-in retail-style "safe sparring" PvP. **Off by default** (`Enabled=false`).

## What it does
A player who runs `/pklite on` is switched from `NPK` to ACE's real `PlayerKillerStatus.PKLite` /
`PKLevel.PKLite` the moment they are inside an admin-configured landblock, and switched back to
`NPK` the moment they leave it, log out, or run `/pklite off`. PKLite is retail's own "no item loss,
no vitae, can still fight" mode - the closest already-built ACE mechanic to a duel/consensual-PvP
system, since this repo confirmed (direct fetch of `Player_Combat.cs` and the `Managers/` directory
listing) that ACE has no separate duel/challenge class at all. A player who never runs `/pklite on`
is never touched, anywhere, ever.

## Why direct property writes, not the PK obelisk path
`PlayerKillerStatus`, `PkLevel` and `PkLevelModifier` are all confirmed `public { get; set; }` on
`WorldObject` (`WorldObject_Properties.cs`), and `PKModifier.cs` (the retail PK-switch obelisk) proves
direct external writes from another class are the normal way ACE itself flips PK state
(`player.PlayerKillerStatus = PlayerKillerStatus.PK; player.PkLevelModifier += PkLevelModifier;`).
This mod writes those two properties directly for the same reason `PKModifier` does - and specifically
**not** through any use-item/activation path, because `PKModifier.CheckUseRequirements` explicitly
refuses to let an already-PKLite player touch a PK obelisk to change status again ("Player Killer
Lites may not change their PK status"). Going through that path would make our own revert-on-leave
logic unable to run once a player was PKLite. Only `NPK <-> PKLite` is ever touched in either
direction; a player who is already `PK`, `Free`, or otherwise flagged is left alone by both `/pklite
on` and the zone sweep.

## Commands
- `/pklite [on|off]` (Player) - opt in, opt out, or with no argument show current opt-in state and PK status.
- `/pklitezone [landblock ...]` (Admin) - with no argument, show the configured zone landblocks (hex) and how many opted-in players are online; with arguments, replace the zone list at runtime (this is **runtime-only** - edit `Settings.json` to make it survive a restart).

## Settings (Settings.json)
- `Enabled` (false) - master switch; nothing is checked or changed while false, and `/pklite` tells the player so.
- `ZoneLandblocks` ([]) - hex landblock list where an opted-in player is switched to PKLite. Empty means no zone anywhere, so `/pklite on` opts in but has no effect until an admin sets one.
- `RevertOnLogout` (true) - if true, a PKLite player (via this mod) is reverted to NPK by a prefix on `Player.LogOut_Inner` before the character saves, so nobody can carry PKLite status out of the zone by disconnecting.
- `SweepSeconds` (5) - how often the online opted-in players are re-checked against the zone list.

## How it decides, every sweep and on logout
For each opted-in online player:
- Inside a zone landblock and currently `NPK` -> set to `PKLite` (both `PlayerKillerStatus` and `PkLevel`), broadcast the property update, whisper the player.
- Outside every zone landblock and currently `PKLite` -> set back to `NPK`, broadcast, whisper.
- Anything else (already `PK`, `Free`, etc.) -> untouched.
- On `LogOut_Inner`, if the player is `PKLite` and opted in, revert to `NPK` first (only if `RevertOnLogout`), then drop them from the opt-in set so a fresh login starts clean.

## Scope reduction from the idea
The idea's "admin-scoped zones" is kept as a flat landblock list (`ZoneLandblocks` / `/pklitezone`),
not a richer per-zone schedule or radius system - that would need a proper zone-entity concept ACE
doesn't have, and the idea itself only asked for a landblock list. The idea also flagged an explicit
precedence rule against `PkGuard`'s landblock-damage-block list and `PkNight`'s full-PK windows; this
mod does not attempt runtime cross-mod coordination (mods here don't reference each other, and none
of the three ship a shared registry to query). Instead: **do not configure `ZoneLandblocks` to overlap
with a `PkGuard`-blocked landblock or a `PkNight`-safe landblock**, and don't run PKLiteZone and a
`PkNight` full-PK window over the same landblocks at the same time - the two are two different PK
concepts and firing together may fight over `PlayerKillerStatus` on transition ticks.

## Risks
- **State-mutating, player-facing.** `/pklite on` changes a live property that affects who can be
  attacked and how death is handled. Test with two characters before trusting the zone in front of
  players: one runs `/pklite on`, walks into a configured landblock, confirms `PlayerKillerStatus`
  shows PKLite (client PK indicator / `@pklevel`-equivalent info if available), and confirms an
  attack from the other opted-in player produces no item loss or vitae, matching retail PKLite.
- **Logout race.** `RevertOnLogout` runs on `LogOut_Inner`, the same hook `MinionCleanup` already
  patches for its own pre-logout sweep; if disabled, a player who disconnects while inside the zone
  keeps PKLite status until they log back in and either leave the zone or run `/pklite off` - by
  design this is opt-in-only exposure, never full PK, so the worst case is an idle PKLite flag, not
  an item-loss risk.
- **Not verified**: whether the client renders any visible PK-status change for `PKLite` the same way
  it does for full PK (this mod was not run against a live client).
- Not compiled by the author; run `check-mod.sh PKLiteZone`.

## Enable later
Copy to the mods folder, set `Enabled` true and a real `ZoneLandblocks` list in `Settings.json`, reload.
