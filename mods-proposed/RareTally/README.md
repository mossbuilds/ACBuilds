# RareTally

Read-only `/raretally` (all players): shows the caller's own lifetime per-tier rare-find counts (`RaresTierOne`..`RaresTierSix`) - numbers ACE already tracks silently for its own real-time-rares pity-timer math but never surfaces to the player - plus, when the server has real-time rares turned on, a rough "time since last rare" (or "time until next pity bonus") derived from `RaresLoginTimestamp`. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure property reads.

## What it does
- `/raretally` prints the six tier counts in one line: `Rare finds - Tier 1: N, Tier 2: N, Tier 3: N, Tier 4: N, Tier 5: N, Tier 6: N.`
- If either `rares_real_time` or `rares_real_time_v2` (server properties, confirmed in earlier rounds and re-confirmed this round via `Corpse.cs`) is enabled and the caller has a `RaresLoginTimestamp`, prints a second line: elapsed time since that timestamp if it is in the past, or remaining time until the pity bonus if it is in the future (the `rares_real_time` variant stores a future "next chance" timestamp while it ticks down; `rares_real_time_v2` stores the timestamp of the last rare actually found - the command handles both without needing to know which mode produced the value).

## What it does not do
- Never writes, increments, or resets `RaresTierOne`..`Six`, `RaresLoginTimestamp`, or any `...Login` field - this is a pure display of values `Corpse.cs`'s `TryGenerateRare` already maintains on every rare drop.
- No tier 7. ACE's own `Player_Properties.cs` has `RaresTierSeven`/`RaresTierSevenLogin` commented out and inert, and `Corpse.cs`'s own `TryGenerateRare` switch has no `case 7:` - this mod mirrors that and does not fabricate a seventh counter.
- No Harmony patches at all (no `[HarmonyPatch]`-decorated method body is used; the `PatchClass` base class is only the settings/command host, same pattern as `DefenseCheck`/`BurdenCheck`).

## Verified against ACE master, re-fetched for this build
- **Declaring file located this round** (the Round 13 idea text flagged this as unlocated, and `Player_Character.cs` was re-checked and also does not have it): `Source/ACE.Server/WorldObjects/Player_Properties.cs`. Each tier counter is a `PropertyInt`-backed public instance property on `partial class Player`:
  ```csharp
  public int RaresTierOne
  {
      get => GetProperty(PropertyInt.RaresTierOne) ?? 0;
      set { if (value == 0) RemoveProperty(PropertyInt.RaresTierOne); else SetProperty(PropertyInt.RaresTierOne, value); }
  }
  ```
  identically for `RaresTierTwo`/`Three`/`Four`/`Five`/`Six` (each backed by its own `PropertyInt`). A commented-out `RaresTierSeven` block sits right next to them, confirming the tier-7 field is defined upstream but genuinely inactive. `RaresLoginTimestamp` is the matching `public int?` property (`PropertyInt.RaresLoginTimestamp`) in the same file. All are confirmed `public`, confirmed readable via a normal getter (no reflection or external-write inference needed for this round - the declaration site itself was found).
- `Source/ACE.Server/WorldObjects/Corpse.cs` - `TryGenerateRare(DamageHistoryInfo killer)` confirms both the write side (`killerPlayer.RaresTierOne++` / `RaresTierOneLogin = timestamp`, once per tier, on a successful rare drop; identical pattern tiers 2-6) and the pity-timer read side: `realTimeRares`/`realTimeRaresAlt` come from `PropertyManager.GetBool("rares_real_time").Item` / `PropertyManager.GetBool("rares_real_time_v2").Item` (both already confirmed server properties in earlier rounds), and when either is on, `killerPlayer.RaresLoginTimestamp` is compared against the current time to decide the pity bonus. This command reads the exact same properties and the exact same field, never recomputing or resetting it.

## Risks
- The declaring file was located this round, resolving the risk flagged in the idea text.
- If a tier-7 rare is ever re-enabled upstream (the commented-out block starts being used), this mod will silently not count it until updated - matches ACE's own current inert state, not a bug introduced here.
- The two real-time-rares modes store semantically different things in `RaresLoginTimestamp` (a future "next chance" time vs. a past "last found" time); the command distinguishes them only by whether the value is in the future or the past relative to now, not by which server property is set - this is correct for both known modes but would need a second look if ACE ever changes that semantics.

## Unverified
- Not compiled by hand - `mods-proposed/check-mod.sh RareTally` is the real check; see STATUS.md for the result.
- Command name `raretally` checked against `%TEMP%\cmds.txt` (327 built-ins): not present.

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/raretally`.
