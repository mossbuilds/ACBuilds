# GeneratorNudge

Admin action that force-resets one stalled generator via the real `WorldObject.ResetGenerator()`
entry point - closing the exact gap Round 9's `GeneratorAudit` (idea 57) explicitly declined to
act on ("no public `ResetGenerator`-style method was found by name search; only the read-only
diagnostic half of the idea survived"). Found this round by fetching
`Source/ACE.Server/WorldObjects/WorldObject_Generators.cs` in full rather than name-searching.

**This mod is NOT read-only. It mutates world state: it destroys objects.** Read the risk section
below in full before enabling it.

## Command

`/gennudge` (Admin, `CommandHandlerFlag.RequiresWorld`). Appraise a generator first (examine it),
then run the command with no arguments - it acts on your last-appraised object, the same lookup
pattern `VendorStock`/`PriceCheck` already use (`Player.RequestedAppraisalTarget` +
`Player.FindObject`).

It prints, before acting:
- `0x<guid> <name> - before: AllProfilesUnavailable=<bool>, CurrentCreate=<n> across <k> profile(s)`
- the full mutation warning (below), every single time, not just once.

Then calls `obj.ResetGenerator()`, then prints:
- `0x<guid> <name> - after: AllProfilesUnavailable=<bool>, CurrentCreate=<n>`
- how many previously-spawned objects were destroyed (or that nothing was spawned, so nothing was
  destroyed).

Pairs with `GeneratorAudit`'s flagged list: run `/genaudit` first, then `/gennudge` on anything
still flagged after checking it by eye.

## API verified against real ACE master source (github-second-brain, ACEmulator/ACE)

`Source/ACE.Server/WorldObjects/WorldObject_Generators.cs` (fetched in full):

```csharp
public virtual void ResetGenerator()
{
    foreach (var generator in GeneratorProfiles)
    {
        generator.Reset();
    }
}
```

Confirmed **public virtual**, an ordinary instance method on `WorldObject` itself - no
internal/protected access, callable directly on any generator reference obtained the normal way.

`Source/ACE.Server/Entity/GeneratorProfile.cs` (fetched in full), `GeneratorProfile.Reset()`:

```csharp
public void Reset()
{
    foreach (var rNode in Spawned.Values)
    {
        var wo = rNode.TryGetWorldObject();
        if (wo != null)
        {
            if (wo.IsGenerator) wo.ResetGenerator();      // recurses into nested generators
            if (wo.Container == Generator) { var container = Generator as Container;
                container?.TryRemoveFromInventory(wo.Guid); }
            wo.Destroy();                                  // destroys every currently spawned object
        }
    }
    CleanupProfile();   // clears Spawned + SpawnQueue, resets NextAvailable to now
}
```

## Safety analysis (read this before enabling)

- **Calling it standalone is not unsafe in the sense of corrupting server state or crashing
  anything.** `ResetGenerator()` is the exact same call ACE's own landblock unload/reload path and
  `Chest.Reset()` already exercise during normal operation - this mod does not reimplement
  generator logic or invent a new code path, it only exposes an existing public entry point on
  demand. `Destroy()` and `TryRemoveFromInventory()` are ordinary public APIs used all over ACE, not
  reserved for a special internal calling context.
- **It IS destructive to world state in a way staff must understand before running it.** Every
  object the generator currently has spawned - **including live, unkilled creatures a player may be
  mid-fight with, and any items already sitting in the world or a vendor's shop** - is
  unconditionally `Destroy()`'d. This is not a kill (no death credit, no loot drop) and not a
  graceful despawn; the object simply disappears, silently, with no message to the affected player.
  A nested generator (e.g. a chest spawned by a generator, itself spawning loot) is reset
  recursively too, wiping its contents as well.
- **A generator idle by design is cheap to nudge if it's actually empty.** `Reset()` on an empty
  `Spawned` dictionary is a no-op loop plus a queue/timer clear - nudging a `GeneratorAudit` false
  positive (an event trigger, a one-shot boss room, a quest-gated spawn with nothing currently up)
  costs nothing. The real risk is nudging a generator that is stalled **and** still has live spawns
  up from before the stall - the command's warning covers both cases, and echoes `GeneratorAudit`'s
  own false-positive caveat.
- **No confirmation step is required by the command itself** - it is a single, deliberate
  admin-typed command (not a mass sweep, not scheduled, not triggered by any event), matching
  `MinionCleanup`'s precedent for a manual admin action. The before/after print is the safeguard:
  staff sees `CurrentCreate` drop to zero and knows exactly what just happened.

## Settings (`Settings.json`, generated on first run)

- `Enabled` (bool, default `false`) - off by default, this is a state-mutating admin action.

## How to test

1. Enable in `Settings.json`.
2. As an Admin-level character, appraise a generator with active spawns (e.g. a monster generator
   in a dungeon with creatures currently up).
3. Run `/gennudge`. Confirm the before/after message prints correct counts, and that the previously
   spawned creatures/items are gone from the world (check visually or via `/genaudit`/`@generatordump`).
4. Repeat on an already-empty generator (`CurrentCreate=0`) and confirm it reports nothing was
   destroyed.
5. Confirm a non-generator or no-appraisal-target case gives the correct error message instead of
   throwing.

## How to enable later

Flip `Enabled: true` in `GeneratorNudge/Settings.json` on the running server; `HotReload: true` in
`Meta.json` means no restart is required for the setting to take effect on the next command call
(the mod's own `Settings.Enabled` gate is read live via `SettingsContainer.Settings`).

## Verification notes

- `cmds.txt` in `%TEMP%` checked (327 built-ins): `gennudge` is free.
- Both source files above were fetched in full via github-second-brain against `ACEmulator/ACE`
  master, not guessed or inferred from a name search - correcting Round 9's miss.
