# DefenseCheck

Read-only `/defensecheck` (all players): shows the caller's *effective* melee, missile, and magic defense - the modified numbers ACE actually uses when resolving an incoming attack against them, never surfaced on the character panel. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure method reads.

## What it does
- `/defensecheck` prints one line: effective melee defense, effective missile defense, and effective magic defense.
- Melee/missile come from `Player.GetEffectiveDefenseSkill(CombatType.Melee)` / `.GetEffectiveDefenseSkill(CombatType.Missile)`.
- Magic comes from `Player.GetEffectiveMagicDefense()`.

## What it does not do
- Does not touch combat resolution, `TakeDamage`, or any attack path - it only calls the same computed-value getters ACE's own combat resolver already calls.
- No Harmony patches at all (no `[HarmonyPatch]`-decorated method body is used; the `PatchClass` base class is only the settings/command host, same pattern as `BurdenCheck`).
- Reports current values only, not a breakdown of what raised or lowered them (raw skill vs. weapon mod vs. imbue vs. stance mod are all folded together inside the two public methods).

## Verified against ACE master, re-fetched for this build
- `Source/ACE.Server/WorldObjects/Creature_Combat.cs` - `public uint GetEffectiveDefenseSkill(CombatType combatType)`. Doc comment: "Returns the effective defense skill for a player or creature, ie. with Defender bonus and imbues". Body: picks `Skill.MeleeDefense` or `Skill.MissileDefense` by `combatType`, applies the matching weapon defense modifier (`GetWeaponMeleeDefenseModifier`/`GetWeaponMissileDefenseModifier`), the burden mod, the matching defense imbue (`GetDefenseImbues`), and - for `Player` specifically - a stance mod (`player.GetDefenseStanceMod()`); zeroes the result if `IsExhausted`. Confirmed `public` instance method on `partial class Creature` (`ACE.Server.WorldObjects`), so it is inherited directly by `Player` and callable as `player.GetEffectiveDefenseSkill(...)`.
- `Source/ACE.Server/WorldObjects/Creature_Magic.cs` - `public uint GetEffectiveMagicDefense()`. Doc comment: "Returns the creature's effective magic defense skill with item.WeaponMagicDefense and imbues factored in". Body reads `GetCreatureSkill(Skill.MagicDefense).Current`, `GetWeaponMagicDefenseModifier(this)`, and `GetDefenseImbues(ImbuedEffectType.MagicDefense)`. Confirmed `public` instance method, no parameters, returns `uint`.
- `CombatType` is `ACE.Entity.Enum.CombatType` (confirmed via the `using ACE.Entity.Enum;`/`using ACE.Entity;` block at the top of `Creature_Combat.cs`, and already resolvable in this repo's mod build setup - `mods-proposed/AmbushStrike/PatchClass.cs` already references `CombatType.Melee`/`CombatType.Missile` the same way through the same `GlobalUsings.cs` pattern used here). Values used: `Melee`, `Missile`.
- Neither `GetWeaponMagicDefenseModifier`, `GetWeaponMeleeDefenseModifier`/`GetWeaponMissileDefenseModifier`, nor `GetDefenseImbues` is called directly by this command - all are already baked into the two public wrapper methods this command calls, the same pattern `BurdenCheck` used for `GetEncumbranceCapacity`/`GetAvailableBurden`.

## Risks
None identified. Both methods are pure computed-value getters that ACE's own combat resolver already calls on every incoming attack; this command never calls `TakeDamage` or touches any combat-resolution path, only reads the two numbers.

## Unverified
- Not compiled by hand - `mods-proposed/check-mod.sh DefenseCheck` is the real check; see STATUS.md for the result.
- Command name `defensecheck` checked against `%TEMP%\cmds.txt` (327 built-ins): not present.

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/defensecheck`.
