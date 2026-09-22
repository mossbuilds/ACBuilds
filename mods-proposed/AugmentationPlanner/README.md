# AugmentationPlanner

Read-only `/augcheck` (all players): prints available XP and, for every `AugmentationType`, whether the caller can
still take that augmentation, how many they have of it, its cap, and (for the attribute and resistance families)
the shared family count against the same cap. Off by default (`Enabled=false` in Settings.json and Meta.json). No
settings beyond the on/off switch, no patches, pure property reads - never calls `DoAugmentation`, never consumes
an augmentation gem or XP.

## Why

Augmentation gems are one-time-consumed. Before a player spends a gem and XP on one, `/augcheck` shows the same
pass/fail conditions `AugmentationDevice.VerifyRequirements` checks internally, without touching an actual gem.

## Verified against ACE master, re-fetched for this build

- `Source/ACE.Server/WorldObjects/AugmentationDevice.cs`
  - `public static Dictionary<AugmentationType, int> MaxAugs` - confirmed public static. Per-type cap: attributes
    (Strength..Self) capped at 10 each but sharing one family counter; resistances (ResistSlash..ResistElectric)
    capped at 2 each, also sharing one family counter; most others capped at 1; a few standalone caps differ
    (BurdenLimit=5, DeathItemLoss=3, BonusSalvage=4, RegenBonus=2, SpellDuration=5).
  - `public static Dictionary<AugmentationType, PropertyInt> AugProps` - confirmed public static. Maps each
    `AugmentationType` to the exact `PropertyInt` the player's current count of that augmentation is stored under.
  - `public static bool AttributeAugmentationSafetyCapEnabled` - confirmed public static
    (`PropertyManager.GetBool("attribute_augmentation_safety_cap")`). **Not used by this command** - it only gates
    the per-attribute `StartingValue >= 96/100` innate-value check inside `VerifyRequirements`, which reads
    `player.Attributes[attr].StartingValue` (a `CreatureAttribute` member not re-opened this round, matching the
    idea entry's own flagged gap - see "Not verified this round" below).
  - `VerifyRequirements(Player player)` read in full to confirm the exact comparisons this command mirrors: family
    counter checked against `MaxAugs[type]` first for attributes/resistances, then the per-type `AugProps[type]`
    count checked against the same `MaxAugs[type]` for every type.
- `Source/ACE.Server/WorldObjects/Player_Properties.cs`
  - `public int AugmentationInnateFamily { get => GetProperty(PropertyInt.AugmentationInnateFamily) ?? 0; ... }`
  - `public int AugmentationResistanceFamily { get => GetProperty(PropertyInt.AugmentationResistanceFamily) ?? 0; ... }`
  - `public long? AvailableExperience { get => GetProperty(PropertyInt64.AvailableExperience); ... }`
  - All three confirmed public instance properties on the `partial class Player`, same file/pattern as
    `LuminanceLedger`'s `AvailableLuminance`/`MaximumLuminance`.
- `Source/ACE.Entity/Enum/AugmentationType.cs`
  - `AugTypeHelper.IsAttribute(AugmentationType)` and `AugTypeHelper.IsResist(AugmentationType)` - both confirmed
    public static, used exactly as `AugmentationDevice` itself uses them to decide which family cap applies.
  - Enum value `41` does not exist in ACE's own numbering (`// missing 41?` comment in source, between `AllStats=40`
    and `FociVoid=42`). `Enum.GetValues<AugmentationType>()` simply never yields it - no special-casing needed.

## Not verified this round (secondary, non-load-bearing)

The idea text flagged the per-attribute `StartingValue >= 100` (or `96` with the safety cap) innate-value check
inside `VerifyRequirements` as a real gate `/augcheck` doesn't reproduce - it reads `player.Attributes[attr].StartingValue`,
and `CreatureAttribute` was not re-opened this build to confirm that member's accessibility. This command's "remaining
slots" answer (family cap + per-type cap) does not depend on it, so it is dropped rather than guessed at: a player
whose family/per-type counts show "available" but who is already at the innate value ceiling will still see
"available" here and get the real error from the game itself on actually using a gem.

## Command name

`augcheck` checked against `%TEMP%\cmds.txt` (327 built-ins): not present.

## Risks

None identified. Pure property reads, no patches - same shape as the already-shipped `BurdenCheck`/`LuminanceLedger`.
Never calls `DoAugmentation`, never touches an inventory item, never spends XP.

## Test

Set `Enabled=true` in Settings.json and Meta.json, run `/augcheck`.
