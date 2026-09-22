# CorpseWatch

`/mycorpse` plus automatic 5-and-1-minute whispers before a player's own corpse decays or opens to looting.

## What it does

- Whenever a player's own corpse spawns (Harmony postfix on `Corpse.EnterWorld`), tracks it in memory keyed by the owning player's guid.
- A background sweep (every `SweepSeconds`, default 15) checks each tracked corpse's `TimeToRot` and whispers the owner once per threshold in `WarnSeconds` (default `[300, 60]` - 5 minutes and 1 minute remaining), only while the owner is online.
- `/mycorpse` (`AccessLevel.Player`) reports on demand how long until the caller's most recently tracked corpse decays or opens, or that it's already been looted / no longer exists.
- Only ever reports on the caller's *own* most recent corpse - never anyone else's.

## What it doesn't do

- Doesn't track multiple corpses per player at once, only the most recent one (retail's own `@corpse` is the same "last corpse" model).
- Doesn't change decay time, looting rights, or any other corpse behavior - purely read-only reporting.
- Doesn't persist tracked corpses across a server restart (in-memory only; a corpse that already exists when the mod loads is not retroactively tracked - it will be picked up the next time that player dies).
- Doesn't warn a player who is offline; if they log back in before the corpse rots, `/mycorpse` still works, but the timed whisper for a threshold already passed while offline is not resent.

## Commands

- `/mycorpse` (Player) - time remaining before your last corpse decays or opens to looting.

## Settings (`Settings.json`)

- `Enabled` (bool, default true)
- `WarnSeconds` (list of int, default `[300, 60]`) - seconds-remaining thresholds that trigger an automatic whisper.
- `SweepSeconds` (int, default 15) - how often the sweep runs.
- `MaxTracked` (int, default 200) - oldest tracked corpses are dropped past this count, so a mass-death event can't grow the dictionary unbounded.
- `AllowMyCorpse` (bool, default true) - master switch for the `/mycorpse` command specifically.

## Risks

- Harmony postfix on `Corpse.EnterWorld()` (public, verified against ACE master) rather than on the protected `Creature.CreateCorpse()` or `Player.Die()`: `CreateCorpse`'s `Corpse` instance is a local variable never returned or stored on a field, so a postfix on `CreateCorpse` cannot recover it without a transpiler. `EnterWorld()` is the first public point where the fully-built `Corpse` (with `VictimId`/`TimeToRot`/`Location` already set) is available as `__instance`, so this is the same "find the nearest public hook" adaptation this repo's process expects when a called method is protected.
- `TimeToRot`/`CreationTimestamp` math (`CreationTimestamp + TimeToRot - Time.GetUnixTime()`) mirrors the same formula ACE's own `Corpse.cs` logging already uses to describe "when does this corpse stop being protected" - it is an estimate for player information only, not a re-implementation of the server's actual decay/looting-permission logic (see `Corpse.HasPermission`/decay elsewhere in ACE, untouched by this mod).
- In-memory tracking only, capped at `MaxTracked`; a dictionary/list under a single lock, sized for normal death rates, not a mass-death event across hundreds of players simultaneously.
- Off by default until Tom enables it in `Meta.json`/deploys.

## How to test

1. `mods-proposed/check-mod.sh CorpseWatch` compiles this mod against the ACE binaries in the .NET 10 SDK image (source-only; nothing is deployed).
2. To try it live (Tom's call, not part of this loop run): copy the built output into a local dev ACE server's `Mods/CorpseWatch` folder, enable it in `Meta.json`, die on a test character, and confirm `/mycorpse` reports a shrinking time and that whispers arrive near the 5- and 1-minute marks.

## How to enable later

Set `"Enabled": true` in `Meta.json` (already the mod-level default) and drop `Settings.json` next to it if non-default settings are wanted; deploying to the running server is a separate, explicit step this loop never takes.
