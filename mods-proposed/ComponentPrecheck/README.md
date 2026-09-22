# ComponentPrecheck

Player command `/compcheck <spellId>` previews whether the caller has the spell
components a cast of that spell would consume - **before** committing to the
cast animation and risking a fizzle mid-fight for a missing reagent.

## What it does

- `/compcheck <spellId>` looks up the spell by its numeric ID (the DAT-file
  spell definition, via `new Spell(uint)`) and reports one of:
  - "Components are not required right now" - `require_spell_comps` is off
    server-wide, or the caller has a safe-components override, so a real cast
    would not touch any component either.
  - "You have all the components this cast needs." - every required component
    is present in at least the required quantity.
  - A list of missing components by name, with "have X, need Y" for each one
    that falls short.
- Off by default (`Meta.json: "Enabled": false`, `Settings.Enabled = false`).

## What it does NOT do

- It never casts anything, never opens the cast animation, and never selects
  a spell for the caster.
- It never burns, decrements, or removes any component or item.
- It does not resolve "the spell currently on your spellbar" - ACE's real
  cast path is driven by a game-action packet from the client (spellbar
  click / hotkey), not a text command argument, and no public accessor for
  "the last spell the client attempted to cast" was found in
  `WorldObjects/Player_Magic.cs` this round. The command instead takes the
  spell ID directly, the same style of argument several stock developer
  commands already use (see `new Spell(spellId)` calls in
  `Command/Handlers/DeveloperCommands.cs`).

## Mutation-safety analysis (the reason this needed extra care)

The idea text named two ACE members:

- `Player.HasComponentsForSpell(Spell spell)` - **confirmed read-only.**
  Body (Player_Magic.cs): calls `spell.Formula.GetPlayerFormula(this)`, then
  for each entry of `spell.Formula.GetRequiredComps()` (a `Dictionary<uint
  wcid, int required>` built purely by counting `Formula.CurrentFormula`,
  `SpellFormula.cs`) compares `required` against
  `Container.GetNumInventoryItemsOfWCID(wcid)` and returns `false` on the
  first shortfall. No write to any item, player property, or the inventory
  collection anywhere in the method. This mod calls it directly.

- `Player.TryBurnComponents(Spell spell)` - **confirmed MUTATING. This mod
  never calls it.** Verified against
  `Source/ACE.Server/WorldObjects/Player_Magic.cs` on `ACEmulator/ACE`
  master (fetched in full this round):
  - Line 1181: `public void TryBurnComponents(Spell spell)`
  - Lines 1183-1184: early-returns only when safe-components is on - i.e.
    when it does NOT return early, it proceeds to consume.
  - Line 1186: `var burned = spell.TryBurnComponents(this);` - resolves which
    components will be burned.
  - Line 1213: `TryConsumeFromInventoryWithNetworking(item, 1);` - inside the
    per-component loop, called once per component actually found in
    inventory. This is the real consumption: it removes a stack of 1 from
    the player's inventory and networks the change to the client. This is
    the exact call the real cast handler uses to spend reagents.
  - Line 1221: sends the same "you have used up your last X" style chat
    message a live cast would send.

  In short: `HasComponentsForSpell` is the check, `TryBurnComponents` is the
  spend. The idea text's own instinct that `TryBurnComponents` "sounds like
  it mutates" was correct, and the mutation is not hypothetical or
  conditional on some rare branch - it is the method's entire purpose.

To report which specific components are short (not just "no"), this mod does
**not** call `TryBurnComponents` to find out - it manually re-derives the
per-component required counts by walking `spell.Formula.CurrentFormula`
directly (public `List<uint>`, same field `HasComponentsForSpell` populates
via `GetPlayerFormula`) and resolving each component's display name from the
static `SpellFormula.SpellComponentsTable.SpellComponents` dictionary
(`SpellComponentBase.Name`, public, `ACE.DatLoader`) and WCID from the public
static `Spell.GetComponentWCID(uint)`. Every one of these is a pure lookup
against cached DAT-file data or a read of `GetNumInventoryItemsOfWCID` - the
same read `HasComponentsForSpell` already performs, done once more per
component purely for the missing-item list.

## Settings

None beyond the master `Enabled` switch. No server properties are read
except `require_spell_comps` (read-only, via `PropertyManager.GetBool`,
exactly as `HasComponentsForSpell`/`TryBurnComponents` already do), and no
`safe_spell_comps`/`SafeSpellComponents` override is written.

## Risks

None identified. The command performs no writes to any world object, item,
or player property - it is a pure read followed by chat output. The only
open question flagged by the idea text (resolving "the currently selected
spell") is answered by taking the spell ID as a command argument instead of
guessing at an unverified accessor.

## How to test

1. Enable in `Settings.json` (`"Enabled": true`) and deploy to a local dev
   server (not covered by this build - source only).
2. `/compcheck <spellId>` for a spell you can cast and have full reagents
   for -> "You have all the components this cast needs."
3. Drop or bank one required component, run again -> that component listed
   as missing with the correct have/need counts.
4. With `require_spell_comps` off (or a safe-components override on) ->
   "Components are not required right now."
5. Confirm no inventory item count changes across any of the above - this is
   the property that matters most, since the bug class this mod is meant to
   avoid is an accidental `TryBurnComponents` call.
