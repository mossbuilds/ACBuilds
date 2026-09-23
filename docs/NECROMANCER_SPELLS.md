# Necromancer path — spell mapping research

Read-only research. No SQL applied, no game state or database changed. Written 2026-09-23 by Moss.
Raw query results: `docs/necromancer-spells-data/*.tsv`.

This document answers one question: **which existing ACE spells (already in the client DAT and the
`ace_world.spell` table) can a "necromancer" path grant to players**, given that ACE cannot add new spell
visuals/icons — every ability the necromancer path uses must be an existing spell id, an existing item/mod
mechanic, or the `RaiseSkeleton` pet mod already proposed.

## Step 1 — necromancer abilities in the book game

Source repo: `C:\files\book\game`. The necromancer/summoning design lives in two places:

- `data/spells.json` (base necromancer spell list, 5 entries)
- `data/spells_extra.json` (extended/late-game necromancer + other class spells, 15 entries, mixed classes)
- `systems/summoning.py` (control-limit and minion-level math, referenced as "Rules 7" / `MASTER_GAME_RULES.md` section 7)
- `data/npcs.json` line 16: NPC role `necromancer_mentor` (Morgan) — confirms necromancer is a real class in the design
- `ACBuilds/docs/AWAKEN_PORT_PLAN.md` Phase 4 already earmarks a "Necromancer path" using `RaiseSkeleton` for
  summons and notes "corpse-based damage approximated with existing war magic or an item spell" — this
  document fills in that approximation with real spell ids.

### Necromancer abilities found

| Ability | Type | Description | Tier/level gate | Source |
|---|---|---|---|---|
| `summon_zombie` | summon | Raise a basic zombie from a corpse | base (mana 40) | `data/spells.json:2` |
| `specialized_zombie` | summon | Raise a zombie retaining some of the corpse's skills | base (mana 80) | `data/spells.json:3` |
| `corpse_explosion` | damage (AoE) | Detonate a corpse for AoE damage scaled to its max HP | base (mana 30) | `data/spells.json:4` |
| `curse_of_weakness` | debuff | Reduce target's Str/Dex for a duration | base (mana 20) | `data/spells.json:5` |
| `custom_skeleton` (base) | summon/utility | Open the Skeleton Editor to design and raise a custom skeleton minion from bones | base (mana 0, UI tool) | `data/spells.json:6` |
| `undead_devotion` | summon (permanent) | Convert corpses within 5 tiles into permanent, non-decaying "devoted citizens" | late (mana 300, level 60+ or post book1_complete) | `data/spells_extra.json:2-5` |
| `undead_devotion_memory_read` | utility/debuff | Cast on a living/undead target to read a 60s surface-thought memory | upgrade (mana 30, Ch.38) | `data/spells_extra.json:66-68` |
| `bone_demon` | summon (ultimate) | One-time-per-playthrough emergency summon of a huge scythe-wielding Bone Demon; consumes all mana + 1000 bones | ultimate/emergency (mana 0, resource-gated) | `data/spells_extra.json:6-9` |
| `zombie_lieutenant` | buff/utility (pet command) | Convert a nearby owned zombie into a Lieutenant that issues squad orders (guard/patrol/follow) | mid (mana 100) | `data/spells_extra.json:10-13` |
| `call_the_dark_sky` | buff (zone-wide) | Summon a storm suspending undead sunlight weakness and boosting dark-resisting magic for 3 in-game hours | late (mana 250, cooldown 600s, requires Summoning Mastery: Intermediate) | `data/spells_extra.json:14-19` |
| `custom_skeleton` (Book2) | summon/utility | Raise a custom skeleton from nearby bones; level scales with caster level + Willpower | mid/Book2 (mana 60) | `data/spells_extra.json:29-32` |
| `bone_armor` | buff (shield) | Up to 3 concurrent 200-damage-absorb shields, 5 mana/sec upkeep each | late/Book2, Death Mantle unlock (mana 1000) | `data/spells_extra.json:33-36` |
| `dark_incarnation` | buff/utility (racial) | Shade-only: body becomes a dark-mana cloud, immune to physical damage, +125% mana regen, more vulnerable to magic; 30s duration, 24h cooldown | racial (mana 0) | `data/spells_extra.json:54-57` |

Supporting mechanics (not spells themselves, but shape the design in Step 4):
- `systems/summoning.py::control_limit(wil, mastery_rank)` — minion cap scales with Willpower and a
  Summoning Mastery rank (+5/+13/+23%). AC has no Willpower attribute; `RaiseSkeleton`'s README already
  substitutes `Self` for it.
- `systems/summoning.py::minion_level` — minion level = `min(corpse_level, caster_level + wil//5)`.
- `systems/summoning.py::decay_ttl` — undead decay faster in daylight (x0.5), slower near a "dark nexus" (x3).

Two spells named in `data/spells_extra.json` (`vortex_arrow`, `blood_mist`) belong to a different class
(archer/ranger, "BOOK2" arrow abilities) and are excluded. `rage_bull_rush`, `rage_of_the_herd`,
`thaumaturge_shift`, `void_arrow`, `health_transfer` are other classes' (Frank/Fury) abilities, also excluded.

**Total necromancer abilities found: 13**, all from `C:\files\book\game\data\spells.json` and
`C:\files\book\game\data\spells_extra.json`.

## Step 2 — the ACE world DB spell table

Query (read-only, `SELECT` only) run against the live VPS via
`ssh ... root@89.117.147.78 "docker exec ace-db mariadb -N -B ace_world -e '...'"`.

- `select count(*) from spell;` → **6266 rows** (`weenie_properties_spell_book` references 2470 distinct
  spell ids at most; not every id in `spell` is a real castable player spell — see caveat below).
- `describe spell;` → columns saved conceptually in `C:\Users\thcst8\.claude\skills\ac-creator\reference\world-db-schema.md`.
  **Important finding: the `spell` table has no `school`, `category`, or `level` column.** The closest
  proxies are `e_Type` (effect type, e.g. `1024` = a Void Magic damage-over-time/bolt family, `512` = a
  drain family, `128`/`16`/`64`/`1`/`2`/`4` = other elemental/curse families), `dispel_School` (a school id
  used only for dispel matching), and `damage_Type`. **Spell level/circle and the exact formula/component
  data are not stored in `ace_world.spell` at all** — those live in the client DAT (the retail `SpellTable`),
  which ACE reads at runtime, not from this SQL table. This table instead holds only the numeric effect
  parameters ACE needs server-side (stat mod values, damage ratios, drain percentages, boost amounts, etc).
  **This means the `spell` table is a supplemental effects table, not a full copy of the client's spell
  list with names/levels/schools; treat any level/school claim below as inferred from spell *name*
  conventions (roman numeral tier suffixes, "Nether"/"Void"/"Drain"/"Curse" naming), not from a DB column.**

Raw dump: `docs/necromancer-spells-data/spell_all.tsv` (id, name, e_Type, damage_Type, dispel_School,
drain_Percentage, boost, min_Power, max_Power, align — all 6266 rows).

Necromancer-flavored candidates (name matches nether/void/drain/curse/dark/shadow/necro/skeleton/dead/bone/corpse):
`docs/necromancer-spells-data/spell_necro_keyword.tsv` — **222 spells**.

### Usage counts (per spell id, summed across three sources)

| Source table | Column | Rows pulled | File |
|---|---|---|---|
| `weenie_properties_spell_book` | `spell` (creature/NPC spellbooks) | 2470 distinct ids | `usage_spellbook.tsv` |
| `weenie_properties_d_i_d` where `type=28` (`PropertyDataId.Spell`, confirmed via `acenum.py PropertyDataId Spell`) | `value` (items/scrolls that cast a spell) | 2736 distinct ids | `usage_did_scrolls.tsv` |
| `weenie_properties_emote_action` where `spell_Id is not null` | `spell_Id` (emotes that cast a spell) | 3342 distinct ids | `usage_emote_action.tsv` |

Combined per-spell usage for the 222 necromancer-flavored candidates: `docs/necromancer-spells-data/necro_candidates_usage.tsv`
(sorted ascending by usage). **Across all 6266 spells in the table, 1521 have usage = 0 in all three sources
(unused). Of the 222 necromancer-flavored candidates, 43 are unused.**

Caveat: "usage = 0" means not referenced by an NPC/creature spellbook, an item's cast-on-use `Spell`
property, or an emote action's `spell_Id` in the current `ace_world` data. It does not prove no player can
ever reach the spell (e.g. via a rare quest reward item not yet checked, or a formula-linked "companion"
spell like a duration/link spell chained off another). Treat "unused" as "very low collateral impact if
granted," not "provably orphaned."

## Step 3 — proposed mapping

Ordering follows the requested preference: (a) usage=0 spells first, (b) Void Magic nether-damage / Life
Magic drain-curse spells, (c) rare monster-only spells. `RaiseSkeleton` is used for every summon-type ability
per the constraint that ACE has no true creature-summon spell effect.

| Necromancer ability | Primary AC spell | Alt 1 | Alt 2 | Rationale |
|---|---|---|---|---|
| `summon_zombie` | **RaiseSkeleton mod** (`/raiseskel`, controlled minion, `mods-proposed/RaiseSkeleton`) | — | — | Not a spell in ACE; the mod already does exactly this (corpse → controlled pet). No spell mapping applies. |
| `specialized_zombie` | **RaiseSkeleton mod**, skill-inherited variant would need mod code (`specialized_zombie_skill_cap` logic), not a spell | — | — | Same as above; "skill inheritance" is pet-init logic, not a castable spell. |
| `custom_skeleton` (both versions) | **RaiseSkeleton mod** | — | — | The "Skeleton Editor" is a UI feature with no AC analogue; the mod's fixed skeleton model stands in. |
| `bone_demon` | **RaiseSkeleton mod**, one-off "feral"-tier spawn at a higher fixed level, gated by a mod cooldown flag | — | — | A one-time emergency summon is pet-mechanic + mod cooldown, not a spell. |
| `zombie_lieutenant` | **MinionOrders mod** (`mods-proposed/MinionOrders`, if it covers squad orders) or a small extension to `RaiseSkeleton` | — | — | Squad-order logic is pet AI, not a spell effect. Flag: confirm `MinionOrders` scope before relying on it. |
| `corpse_explosion` | **id 5544 "Nether Blast I"** (e_Type 1024, Void Magic bolt/AoE family; usage 0) | id 5371 "Festering Curse I" (e_Type 1024, usage low — check `necro_candidates_usage.tsv`) | id 1237 "Drain Health Other I" (usage 84, drain-flavored fallback if Nether Blast proves monster-only) | Void Magic "Blast"-type spells are AC's closest thing to an AoE nether burst; usage=0 means granting it changes nothing for existing NPCs/items. |
| `curse_of_weakness` | **id 5379 "Weakening Curse I"** (e_Type 1024, usage 3 — near-zero) | id 3 "Weakness Other I" (Life Magic, usage unknown — check TSV) | id 1846 "Curse of Black Fire" (usage 0) | "Weakening Curse" is literally a Str/Dex-style debuff family in the Void school naming convention; low usage keeps collateral minimal. |
| `undead_devotion` | **id 2068 "Brittle Bones"** (usage 0) as a *renamed* placeholder debuff/mark-on-target effect (not a real "convert corpse permanently" mechanic — ACE has none) | id 3835 "Leviathan's Curse" (usage 0, boost -150 — heavy debuff, could reflavor as a "binding" mark) | id 4194 "Magical Void" (usage 0) | No AC spell converts a corpse into a permanent citizen; recommend implementing as a `RaiseSkeleton`-style mod feature (a non-decaying flag) rather than a spell. The spell id above is only useful as a visual/mechanical stand-in for "marking" a corpse, not the conversion itself. **No good spell match — flag below.** |
| `undead_devotion_memory_read` | **id 3801 "Shadow Touch"** (usage 0) | id 3802 "Shadow Reek" (usage 0) | id 4016 "Shadow's Heart" (usage 0) | Utility/debuff-flavored, unused, thematically dark-touch; the "memory read" text output has to be mod-side (chat message), the spell only supplies the cast animation/target-lock. |
| `bone_armor` | **id 4117-4123 "Dark Shield" I-VII** (all usage 0) | id 5754-5756 "Shroud of Darkness (Magic/Melee/Missile)" (all usage 0) | — | "Dark Shield" is an absorb-shield-flavored name family, entirely unused (7 tiers free to grant across level bands). |
| `call_the_dark_sky` | **id 3894 "Dark Persistence"** or id 3896 "Dark Equilibrium" (buff-flavored, check usage in TSV) | id 3897 "Dark Purpose" | id 3235 "Dark Power" (usage 0) | No AC spell affects zone-wide sunlight/weather; recommend implementing the "suspend undead sunlight weakness" mechanic as a mod timer keyed off casting one of these buff spells, not the spell doing the real work. |
| `dark_incarnation` | **id 5427-5430, 6074 "Void Magic Aptitude" family** (usage 0, several tiers) reflavored as a self-buff, or id 3235 "Dark Power" (usage 0) | id 1469-1474 "Hermetic Void I-VI" (usage 0) | — | The "immune to physical, vulnerable to magic" swap is a stat-mod combination no single AC spell does exactly; closest is a Void aptitude/self-buff spell used as the visual + partial mechanic, with a mod applying the actual damage-type immunity/vulnerability swap. |

### Abilities with no good AC spell match (flagged)

- **`undead_devotion`** (permanent corpse-to-citizen conversion) — no AC spell creates a permanent NPC from
  a corpse. Map to a `RaiseSkeleton`-style mod feature (non-decaying `MinionMinutes = 0` flag on a specific
  spell cast, i.e. the spell only triggers the mod's permanent-flag logic).
  it deviates from "usual" RaiseSkeleton since it should feel rarer/late-game — a mod-side level/cooldown
  gate is recommended.
- **`bone_demon`** — same issue: a one-time massive summon has no spell equivalent; must be `RaiseSkeleton`
  mod logic with a cooldown flag and a bigger fixed-level "feral"-tier weenie (parallel to the existing
  `raiseskelferal` class in `RaiseSkeleton`'s SQL).
- **`zombie_lieutenant`** — squad orders are pure pet-AI, not a spell; needs `MinionOrders` (or a new
  `RaiseSkeleton` extension) rather than a spell mapping.
- **`call_the_dark_sky`** — zone-wide weather/resistance effects don't exist as a single-target or even
  ground-effect spell in AC; any implementation is a mod, with a spell cast only as the visual trigger.

## Step 4 — granting mechanism design (design only, no implementation)

- **Gating:** `mods-proposed/PathChoice` (`Meta.json`, `Mod.cs`, `PatchClass.cs`, `Settings.cs`) is the
  existing mechanism: a quest stamp (e.g. `path_necromancer`) gates access to an NPC trainer. The
  necromancer path should reuse this pattern exactly as `AWAKEN_PORT_PLAN.md` Phase 4 already assumes —
  Morgan (the `necromancer_mentor` NPC role from `data/npcs.json`) is the natural trainer weenie to attach
  the stamp check and the spell-teaching emotes to.
- **Adding a spell to a player's spellbook:** `EmoteType.TeachSpell = 27` (confirmed via
  `acenum.py EmoteType`, matches against "spell" in the enum name; the only other spell-casting emote types
  are `CastSpell = 14` and `CastSpellInstant = 19`, which make the NPC cast a spell rather than grant it, and
  `PetCastSpellOnOwner = 73`, which is pet-only). A necromancer trainer NPC should use a `TeachSpell` emote
  chain (one action per spell id, or a script gated on the `path_necromancer` quest stamp) rather than
  `CastSpell`.
- **Player-castable vs. monster-only spells:** the schema has no explicit "player castable" flag; the
  practical signal is whether the spell already appears in `weenie_properties_spell_book` for *player-usable
  item* weenies or has a normal Life/War/Void Magic component-and-formula footprint in the client. Several
  candidates above (the "Bael'zharon's..." family, ids 5332-5336, "Entry to the Accursed Mausoleum..." /
  "Travel to the Prodigal Shadow Child's..." series, and named "X's Curse" boss-signature spells like
  `Gertarh's Curse`, `Karenua's Curse`, `Matron's Curse`, `Sath'tik's Curse`) read as **boss/monster-only or
  portal-recall spells** by name convention (unique named-boss prefixes, or "Entry"/"Travel"/"Crossing" =
  portal-tie spells, not combat spells) and should **not** be granted to players without testing them on the
  local server first — they may lack the player-side formula/component wiring even though they exist as
  rows in `spell`. None of the ids recommended as *primary* mappings above fall in this suspect group; the
  "Alt 2" and "Alt 3" columns are safer to swap in if a primary pick turns out to be monster-only after an
  in-game `/ci <spell as scroll>` test.

## Caveats (repeated for visibility)

1. The `ace_world.spell` table has no level/school/category columns — level and school claims here are
   inferred from spell name and `e_Type`, not verified DB fields. Verify final picks in-game before shipping
   (per `ac-creator` workflow: `check` → `apply` to **local** first).
2. "Usage = 0" is computed from three tables only (creature spellbooks, item cast-on-use `PropertyDataId
   Spell`, and emote `spell_Id`); it is a strong signal, not a proof of total unreachability.
3. No SQL, weenie, or mod was applied or modified. This document and its TSVs are the only new files.
