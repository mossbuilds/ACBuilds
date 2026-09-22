# GeneratorAudit

Read-only Sentinel scan across all loaded landblocks flagging generators whose profiles all look
unavailable at every look, for longer than `ScanSeconds` straight - the generator-side analogue of
StuckVendorWatch (idea 54), distinct from the stock `@generatordump` (one hand-picked generator's
raw property dump).

**Never touches, resets, or regenerates a generator. Pure reads via a polling timer, no Harmony
patches.**

## Command

`/genaudit [landblock]` (Sentinel). Optional 4-hex-digit landblock filters the report to one
landblock (e.g. `/genaudit 9EE5`). Every response - including "no results" - repeats the
false-positive caveat: a generator idle by design (an event trigger, a one-shot boss room, a
quest-gated spawn) will show up here too.

## Verification done before writing this mod

- `GeneratorProfile.cs` **was located and fetched in full**: `Source/ACE.Server/Entity/GeneratorProfile.cs`
  in ACEmulator/ACE (not under `WorldObjects/` as the idea guessed - that guessed path is what 404'd
  during the idea's research). Read directly, not from a changelog/PR summary.
  - `IsPlaceholder`, `IsMaxed`, and `IsAvailable` are all confirmed **public** auto-properties on
    `GeneratorProfile` itself. `Probability` does not exist as a member of `GeneratorProfile` (no
    such field/property in the file); `WeenieClassId` (the closest equivalent to "WCID") is a
    confirmed public property. This closes out the idea's "accessibility unverified" flag for these
    names.
  - The mod does **not** read any of `GeneratorProfile`'s own members directly regardless - per the
    idea's scope, it only calls the five confirmed-public `WorldObject`-level members:
    `IsGenerator`, `GeneratorProfiles` (for `.Count`), `CurrentCreate`, `AllProfilesMaxed`,
    `AllProfilesUnavailable`.
- **No public timestamp exists** anywhere in `GeneratorProfile.cs` or `WorldObject_Generators.cs`
  for "how long has `AllProfilesUnavailable` been continuously true for this generator."
  `GeneratorProfile.NextAvailable` is public but per-profile and forward-looking (when *that one*
  profile becomes available again) - it does not aggregate across all profiles the way
  `AllProfilesUnavailable` does, and no `...Since` equivalent exists at the `WorldObject` level.
- **Stall duration is therefore self-tracked in memory**, the same pattern DecayClock's
  `PatchClass.cs` uses for its per-pet decay clock: a `Dictionary<uint, DateTime>` keyed by
  generator guid, storing the UTC instant each generator was *first* observed with
  `AllProfilesUnavailable == true`. The entry is removed the moment a scan finds the generator no
  longer all-unavailable (recovered), or the generator is no longer present in a scan (unloaded).
  A generator is only flagged once `now - firstSeen >= ScanSeconds`, so a generator caught
  mid-cycle on a single unlucky snapshot never false-positives - it has to read as stalled at every
  look across the whole window, matching the idea's exact wording ("never just caught mid-cycle").
  This state is memory-only and resets on mod/server restart, same tradeoff DecayClock accepts.
- `cmds.txt` in `%TEMP%` checked: `generatordump` is the only related command present (the stock
  one this mod is explicitly distinct from); `genaudit` is free.

## Settings (`Settings.json`, generated on first run)

- `Enabled` (bool, default `false`) - off by default per the idea.
- `ScanSeconds` (int, default `60`, minimum `30` enforced) - both the timer cadence and the minimum
  continuous-stall duration required before a generator is flagged.

## Risks (unchanged from the idea)

A generator legitimately idle by design will still show up here as a false positive - there is no
way to distinguish "broken" from "intentionally quiet" from the public API alone, which is why the
caveat prints on every single response, and why this mod never acts on what it finds.
