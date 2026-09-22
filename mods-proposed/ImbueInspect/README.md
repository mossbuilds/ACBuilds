# ImbueInspect

Idea 75 (Round 12, last idea of the round). Read-only player command answering
"what does this weapon's imbue actually do", in plain English.

## What it does

`/imbuecheck` - reads the caller's last-appraised object (same pattern as
PriceCheck/VendorStock/ItemSetPreview/TinkerHistory: `Player.RequestedAppraisalTarget`
resolved with `Player.FindObject(..., SearchLocations.Everywhere, ...)`, since
`CommandHandlerHelper` is `internal` and not visible outside `ACE.Server`). If the
resolved item is a weapon (`MeleeWeapon`/`MissileLauncher`/`Missile`/`Caster`
`WeenieType`), calls `WorldObject.GetImbuedEffects()` once and prints the plain-English
name of every active flag, plus the resistance type each rending effect boosts damage
against.

## What it does NOT do

- Does not report the *magnitude* of any imbue (rend damage %, crit-rate increase).
  Those numbers live in the accurate-imbue-formula section further down
  `WorldObject_Weapon.cs` (`GetImbuedInterval` and neighbors) and were not re-verified
  as a clean, cheap public read this round - out of scope per the idea's own risk note.
- Never modifies the item or the player. No patches, no state, nothing written to disk.

## Accessibility findings (re-verified this round, full-file fetch of
`Source/ACE.Server/WorldObjects/WorldObject_Weapon.cs` from ACEmulator/ACE master)

- `public ImbuedEffectType GetImbuedEffects()` (line 488) - confirmed public instance
  method, returns the bitwise-OR of `PropertyInt.ImbuedEffect` through `ImbuedEffect5`.
- `public bool HasImbuedEffect(ImbuedEffectType type)` (line 498) - confirmed public,
  but its body is `ImbuedEffect.HasFlag(type)`, where `ImbuedEffect` (singular, no
  number) is a separate public property (`WorldObject_Properties.cs` line 3309) backed
  by **only** `PropertyInt.ImbuedEffect` - slot 1. It does not consult slots 2-5, so it
  would silently miss any effect imbued into those slots. This is a genuine gap between
  the two public members, not an accessibility problem: both are public, but
  `HasImbuedEffect` is not equivalent to "does `GetImbuedEffects()` include this flag".
  **Adaptation**: the mod calls `GetImbuedEffects()` once and checks each named flag
  against that result with `.HasFlag()` directly, never calling `HasImbuedEffect`, so it
  reports every active slot honestly instead of reproducing the gap.
- `ImbuedEffectType` (`Source/ACE.Entity/Enum/ImbuedEffectType.cs`, fetched in full) is a
  public `[Flags] enum : uint` with **15 named values**, not the ~11 the idea listed:
  `CriticalStrike`, `CripplingBlow`, `ArmorRending`, `SlashRending`, `PierceRending`,
  `BludgeonRending`, `AcidRending`, `ColdRending`, `ElectricRending`, `FireRending`,
  `MeleeDefense`, `MissileDefense`, `MagicDefense`, `Spellbook`, `NetherRending`, plus
  three high-bit values `IgnoreSomeMagicProjectileDamage`, `AlwaysCritical`,
  `IgnoreAllArmor`, and `Undef = 0`. The idea's text ("crit strike, crippling blow, the
  8 rending types") undercounted the real flag set - this mod reports all of them, not
  just the ones the idea named.
- `WorldObject.GetRendDamageType(DamageType)` (public static, same file) confirmed the
  rending-to-damage-type mapping used for each rending effect's plain-English phrase.

Nothing named in the idea turned out to be protected/private/internal; the only
adjustment was working around the `HasImbuedEffect` slot-1-only gap and reporting the
enum's real, larger value list.

## Settings

None beyond the standard `Enabled` (default `false`).

## How to test

1. `mods-proposed/check-mod.sh ImbueInspect` to confirm it compiles.
2. To try it live (not part of this task - source only): copy the built DLL + Meta.json
   into a local ACE server's `Mods/ImbueInspect/` folder, flip `Enabled: true` in
   Meta.json (or its runtime settings file), restart/hot-reload, appraise an imbued
   weapon, then `/imbuecheck`.

## How to enable later

Off by default (`Enabled: false` in Meta.json and `Settings.json`). Flip `Enabled` to
`true` and hot-reload or restart to turn it on; no other configuration needed.
