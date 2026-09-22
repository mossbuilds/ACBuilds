# SpellFizzleForecast

Player command `/fizzlecheck <spellId>` previews the caller's fizzle chance
for a spell **before** committing to the cast, using ACE's own real
fizzle-chance formula - the magic-school counterpart to the already-shipped
`ComponentPrecheck`.

## What it does

- `/fizzlecheck <spellId>` looks up the spell by its numeric ID (the
  DAT-file spell definition, via `new Spell(uint)`) and reports:
  - the caller's current skill in the spell's magic school,
  - the spell's power (difficulty),
  - the fizzle chance and success chance as percentages, computed by calling
    `SkillCheck.GetMagicSkillChance` with those two numbers - the exact same
    function and exact same two inputs the real cast path uses.
- Off by default (`Meta.json: "Enabled": false`, `Settings.Enabled = false`).

## What it does NOT do

- It never casts anything, never opens the cast animation, and never rolls
  a random number.
- It does not resolve "the spell currently on your spellbar" - like
  `ComponentPrecheck`, ACE's real cast path is driven by a game-action
  packet from the client, not a text command argument, and no public
  accessor for "the spell about to be cast" was found in
  `WorldObjects/Player_Magic.cs`. The command instead takes the spell ID
  directly, the same style `ComponentPrecheck` (idea 43) already uses.

## Accessibility findings (this round's verification)

All four ACE members this mod touches were re-verified against a fresh
fetch of ACEmulator/ACE master via github-second-brain:

- `SkillCheck.GetMagicSkillChance(int skill, int difficulty)` -
  `public static double` (`Source/ACE.Server/WorldObjects/SkillCheck.cs`,
  whole file is 20 lines). Body: `return GetSkillChance(skill, difficulty,
  0.07f);` - `GetSkillChance` is also `public static double`, and its own
  body is a one-line logistic curve
  (`1.0 - (1.0 / (1.0 + Math.Exp(factor * (skill - difficulty))))`, clamped
  to `[0,1]`). **Confirmed pure and non-mutating**: no field access, no
  `ThreadSafeRandom`/RNG call, no state read or written, nothing sent over
  the network - it is math over its two arguments and nothing else. This is
  distinct from the real cast-check method
  (`Player.GetCastingPreCheckStatus`, `Player_Magic.cs`), which calls this
  same pure function and THEN separately rolls a random number against the
  result and fires the player-visible fizzle animation/message as a side
  effect. This mod never calls `GetCastingPreCheckStatus` or anything that
  rolls that random number.
- `Spell.Power` - `public uint` (`Source/ACE.Server/Entity/SpellProperties.cs`),
  `get => _spellBase.Power` - a pure passthrough to DAT-loaded spell data,
  no setter exposed. Confirmed this is the exact value the real cast path
  reads as `difficulty` (`var difficulty = spell.Power;` in
  `GetCastingPreCheckStatus`, `Player_Magic.cs`, confirmed via direct fetch).
- `Spell.GetMagicSkill()` - `public Skill GetMagicSkill()`
  (`Source/ACE.Server/Entity/Spell.cs`), a pure switch over `Spell.School`
  (itself `public MagicSchool`, `SpellProperties.cs`,
  `get => _spellBase.School`) returning the matching `Skill` enum value.
  Used here in place of `Creature.GetCreatureSkill(MagicSchool)` (also
  `public`, `Creature_Skills.cs`, and itself just a switch that calls the
  `Skill`-typed overload) - both resolve to the same skill; this mod calls
  `player.GetCreatureSkill(spell.GetMagicSkill())` directly.
- `Creature.GetCreatureSkill(Skill skill, bool add = true)` -
  `public CreatureSkill` (`Source/ACE.Server/WorldObjects/Creature_Skills.cs`),
  confirmed via full-file fetch. `CreatureSkill.Current` (public `uint`) is
  read the same way `Player_Magic.cs`'s own real cast-path callers do
  (`GetCreatureSkill(spell.School).Current`) and the same way this repo's
  already-shipped `NetWorthCheck`/`ItemChargeWatch` read other public
  `CreatureSkill`/`WorldObject` properties externally.

Nothing named by the idea text turned out to be protected, private, or
internal - no adaptation was needed.

## Settings

None beyond the master `Enabled` switch.

## Risks

None identified. The command performs no writes to any world object, item,
or player property, makes no RNG call, and sends no packet other than the
chat lines it prints - it is a pure read of DAT-loaded spell data and the
caller's own skill, run through the same pure formula the real cast path
uses, followed by chat output.

## How to test

1. Enable in `Settings.json` (`"Enabled": true`) and deploy to a local dev
   server (not covered by this build - source only).
2. `/fizzlecheck <spellId>` for a spell you can cast - compare the reported
   fizzle % against repeated real casts of that spell (over enough attempts
   to average out) to sanity-check the number lines up.
3. `/fizzlecheck <spellId>` for a spell far above your current skill in
   that school - fizzle % should be high (approaching 100%); for a spell
   well within your skill, it should be low (approaching 0%).
4. `/fizzlecheck 999999999` (an invalid spell ID) -> "No spell found for
   spell ID 999999999."
5. Confirm no cast animation plays, no mana is spent, and no chat message
   other than this command's own output appears across any of the above -
   this is the property that matters most, since the risk class this mod
   is meant to avoid is accidentally calling the real, RNG-rolling
   cast-check method instead of the pure formula.
