# SpellInfo (proposed, off by default)

Read-only admin lookup tool, meant to be used before wiring a spell id into RaiseSkeleton, MinionOrders or
CorpseBurst's `*SpellId` settings.

## Commands (Admin only)
- `/spellinfo <id>` - name, school, category, MetaSpellType, power, base mana, non-component target type,
  its formula (component names, not just ids), and a "player-castable: yes/no/unknown" guess.
- `/spellinfo find <text>` - up to `FindLimit` (default 20) spell ids whose name contains `<text>` (case
  insensitive).

## Where the data comes from (verified against ACE master)
- `DatManager.PortalDat.SpellTable.Spells` - `Dictionary<uint, SpellBase>`, the client DAT spell table
  (`Source/ACE.DatLoader/FileTypes/SpellTable.cs:14`).
- `SpellBase` fields read: `Name`, `School`, `Category`, `MetaSpellType`, `Power`, `BaseMana`,
  `NonComponentTargetType`, `Formula` (`Source/ACE.DatLoader/Entity/SpellBase.cs:9-40`).
- `DatManager.PortalDat.SpellComponentsTable.SpellComponents` - `Dictionary<uint, SpellComponentBase>`, used
  to turn each formula id into its component name (`Source/ACE.DatLoader/FileTypes/SpellComponentsTable.cs:24`,
  `Source/ACE.DatLoader/Entity/SpellComponentBase.cs:7-14`).
- Reference count: a plain `WorldDbContext` and `ctx.WeeniePropertiesSpellBook.Count(r => r.Spell == id)`
  (`Source/ACE.Database/Models/World/WeeniePropertiesSpellBook.cs:9-30`, DbSet declared at
  `Source/ACE.Database/Models/World/WorldDbContext.cs:121`). This is a plain scan of a table with a few
  thousand rows, run once per on-demand admin command - cheap enough to always run; wrapped in try/catch so a
  DB hiccup never breaks the rest of the report.

## "player-castable" heuristic
ACE has no explicit "is this a real player spell" flag. The command guesses from the DAT data alone:
- No formula (no reagent components) **and** `NonComponentTargetType != None` -> reported "no (looks
  NPC/monster-only)". This is the pattern for spells that only ever fire from monster innate attacks or item
  procs, never from a spellbook cast.
- No formula but `NonComponentTargetType == None` -> "unknown" (could be an untargeted/self buff with no
  reagents, which does happen for some player spells).
- Has a formula -> "yes", since a reagent-component formula is what the client actually uses to let a player
  cast the spell from their spellbook.
This is a heuristic, not a guarantee - always confirm with `/spellinfo <id>` -> `/addspell <id>` -> cast it
in game before flipping any mod's `*SpellId` setting on.

## How to test
1. `/spellinfo find nether` (or any keyword) to find candidate ids, or use the ids already suggested in
   RaiseSkeleton/MinionOrders/CorpseBurst's README "Spell binding" sections.
2. `/spellinfo <id>` - check `player-castable` and the formula look sane.
3. `/addspell <id>` (ACE built-in dev command) on a test character, then cast it from the spellbook.
4. Only once it casts cleanly, set the matching mod's `*SpellId` in its `Settings.json` and restart that mod.

## Settings.json
`Enabled` (false), `FindLimit` (20).

## Risks
None to live gameplay - this mod only reads DAT tables already loaded by the server and does a read-only DB
count; it does not modify anything.

## Not compiled at time of writing (fill in once verified)
See `STATUS.md` for the current compile result.
