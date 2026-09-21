# AWAKEN_PORT_PLAN.md - porting the Twilight Throne game into the ACE server

Plan only. No mods, content or SQL are written by this document. Written 2026-09-21 by Moss.
Sources: `C:\files\book\game\docs` (BOOK_BIBLE, STORY_BEATS, ZONES, BOOK2/3 docs, SPEC), `rules_output_haiku\MASTER_GAME_RULES.md`,
and what the ACBuilds servers are known to support (ac-creator skill, ACBuildsAdmin, the 26 proposed mods in `mods-proposed/`).

## 0. What "ported" means
The Twilight Throne game (a pygame necromancer RPG built from the Awaken Online books, 3 books, ~32 quests, 47 NPCs, 62 monsters, 10+ zones per book)
becomes playable on the ACE servers as three kinds of content:

| Game concept | In ACE | Built with |
|---|---|---|
| **NPCs** (Jerry, Morgan, Rex, guards, the Old Man ...) | weenies (creatures) placed in the world, dialogue and quest logic as emotes | ac-creator: weenie SQL, emotes, landblock instances |
| **Events** (Marketplace Uprising, Alexion's Siege, heists, night events) | quests + generators + timed event mods (WorldBoss, KillRace, PkNight, TreasureHunt, AnnounceEvents already exist as source) | SQL quests/generators + mods |
| **Classes** (Necromancer, Rogue/Thief, Fighter, Archer, Mage, Light Crusader) | ACE has no class system: a class becomes a *path* = skill presets + gear + titles + class-gated quests, with a small mod for the mechanics ACE cannot do with data | SQL items/quests + a `Paths` mod |
| **Zones** (Tunnel & Cave, Lux, Sow's Snout, Graveyard, Manor ...) | existing ACE dungeons/towns re-used as stage sets, named and populated for the story (no new terrain or models are possible from SQL) | landblock instances, portals |

## 1. Hard constraints (learned on this server; they shape every phase)
1. **No new art.** Setup/Icon/ClothingBase ids must be real ones from the AC DATs. Every NPC and monster reuses an existing model; minions, skeletons and Death Knights are chosen from existing undead/skeleton models.
2. **No new spells.** Spell effects come from the client's DAT. Necromancy is approximated with existing spells, item spells, pets and mods (see risk R2).
3. **The client owns movement.** No speed/"skill" mods for movement.
4. **Headset memory.** Standalone Quest builds run out of memory in busy towns (Shoushi). Each phase keeps object counts low, keeps set-pieces outside towns, and never adds many objects at once.
5. **Nothing deploys without Tom.** Every phase ends in local/proposed content; a deploy to the VPS happens only when Tom says so, after a backup (existing deploy path).
6. **Data lives in git.** SQL in `custom-content/`, mods in `mods-proposed/` then `mods/`, converters in `tools/`.

## 2. Decisions needed from Tom (defaults chosen so work can proceed)
| # | Decision | Default I will use unless told otherwise |
|---|---|---|
| D1 | Character/lore names: the book's characters and dialogue are copyrighted. Reuse them, or keep the story structure with original names and original lines? | **Original names and original dialogue, same story beats** (a private server for friends is lower risk, but a shared/public one is not). Names can be mapped back later in one table. |
| D2 | Where does it live? Both servers (9000 stock, 9100 VR) share `ace_world`, so content appears on both. | Build for the shared world DB; test on the local stack first. |
| D3 | Which zones are stage sets? | Reuse existing AC dungeons and quiet towns away from Shoushi (headset memory); final picks in Phase 1. |
| D4 | Necromancer minions: pets or generators? | Pets (Phase 0 spike decides). |

## 3. The phases (Book 1 first)

### Phase 0 - Foundations and feasibility (no story content yet)
Goal: prove every mechanic the story needs works in ACE before any content is written.
- **Spike A: quests and dialogue.** One NPC with a 3-step chain (talk, kill count, hand-in, reward), branching emotes, a repeat/erase path. Confirms the emote/quest patterns in the ac-creator reference.
- **Spike B: kill tasks and generators** with a small respawning group away from a town.
- **Spike C: minions.** Test ACE combat pets (a pet weenie that follows and fights, a cap on how many) as the necromancer's zombies/skeletons; record limits (count, level scaling, despawn).
- **Spike D: class gating.** Test skill-locked quest steps, item requirements and titles as the "class" marker.
- **Spike E: existing event mods.** Try WorldBoss/KillRace/PkNight/AnnounceEvents on the local stack (they are compiled but untested in game).
- **Converter.** Design `tools/awaken_port.py`: reads the game's `data/*.json` (npcs, monsters, quests, items, dialogue) and emits ACE SQL through the ac-creator generator. Book 1 has structured data already, so most content is generated, then hand-tuned.
- **Mapping tables.** Game level -> AC level bands, game stats (Str/Dex/Int/Wil/Vit/End) -> AC attributes, game HP/damage -> AC creature values, game currency -> pyreals.
- Deliverable: a feasibility report (what works, what is approximated), the converter design, and the mapping tables. Exit: Tom reviews the report.

### Phase 1 - Book 1 world skeleton (zones and portals)
Book 1 zones (from ZONES.md): Tunnel & Cave, Lux (city, later the Twilight Throne), the Sow's Snout inn, Graveyard, Stable/Guardhouse District, the Manor, the Marketplace, the Forest, the Battlefield outside the city.
- Pick an existing AC location for each (dungeon for the cave, a small quiet town for Lux, a graveyard-style outdoor area, a building for the inn and the manor), all away from Shoushi.
- Portals/recall between them, a "Lux" hub with a name and signage NPCs (few objects).
- Deliverable: a zone table (game zone -> AC landblock/cell), portals in SQL, a test-walk checklist. Exit: a character can travel the whole Book 1 route.

### Phase 2 - Book 1 NPC roster
Book 1 cast (BOOK_BIBLE sec. 5): the old man/deity, the guide/mentors (Jerry the innkeeper-thief, Morgan the necromancer mentor, Rex the weapons trainer), the stable master and his wife, the noble knight (level 192 boss), city guards, the antagonist commander and his army, plus filler townsfolk (renamed per D1).
- One weenie per NPC with a reused model, level per the mapping tables, and idle/greeting emotes.
- Vendors and trainers use the existing vendor/emote patterns (Jerry's shop, Rex's weapon vendor).
- Deliverable: ~12 NPC weenies + ~10 monster weenies (graverobbers, guards, soldiers, manor guards, the boss) in SQL, placed in the Phase 1 zones. Exit: every NPC talks and every hostile can be fought.

### Phase 3 - Book 1 quest chain (Q01-Q17)
The 17 main quests from STORY_BEATS.md, built in three groups, each ending in a playtest:
- **3a. Arrival (Q01-Q04):** awakening in the tunnel, the road to the city, first meeting at the inn, the first job and training.
- **3b. Apprenticeship (Q05-Q11):** the graveyard job, the mentor's test and "real test", first summons and graverobbers, the stakeout, the assassination job at the stables, building the first army.
- **3c. Conquest (Q12-Q17):** the noble conspiracy, the manor heist, the marketplace uprising, taking the throne, the six divisions, and the commander's siege.
- Side content and the bounty board (already sketched in STORY_BEATS "Side Content").
- Deliverable: quests as SQL (`sql/quests`, emotes on the giver weenies) generated by the converter, plus hand-written branches. Exit: a fresh character can play Q01 to Q17 without a dead end.

### Phase 4 - Book 1 classes (paths)
- **Necromancer path:** summon-style minions via the Phase 0 pet mechanism; a "control limit" rule (minions capped by Self/Willpower-equivalent) via a small mod if pets alone cannot enforce it; corpse-based damage approximated with existing war magic or an item spell.
- **Rogue/Thief path:** stealth and sneak-attack approximated with existing skills and items; lockpicking as an item/quest gate.
- **Fighter, Archer, Mage:** skill presets and starter kits from the trainer NPCs (`class_trainers` in the book-side game is the design template).
- **Path mod (`Paths`)**: one mod holding the mechanics data cannot do: path choice command, minion cap, path-gated quest checks. Source only in `mods-proposed/` first.
- Deliverable: path definitions, starter gear SQL, the `Paths` mod source (compiled, not deployed). Exit: each path can complete Book 1 and feels different.

### Phase 5 - Book 1 events
- **Marketplace Uprising** and **the Siege** as scheduled/announced events: a wave-based boss fight built on the WorldBoss/KillRace patterns, run outside towns, few objects at a time (headset limit), with rewards and a leaderboard.
- **Graveyard night event** (time-limited undead spawn) and **the heist** (an instanced-feeling room using an existing dungeon).
- Deliverable: event definitions (SQL) + event mods enabled by an admin command, all off by default. Exit: an admin can run each event start to finish.

### Phase 6 - Book 1 integration, balance, release gate
- Playtests with two characters (one per path), balance pass on levels, drops and XP curves, headset performance pass.
- Docs: player guide, admin guide, rollback notes. A backup-first deploy plan.
- Deliverable: everything committed; a written release checklist. **Deploy only when Tom approves.**

### Phases 7-9 - Books 2 and 3 (outline, planned in detail after Phase 6)
- **Phase 7: Book 2 (Precipice):** zones from ZONES_BOOK2 (canyon road, falcon's hook and the invasion, more zones), new monsters (werewolves, minotaur line, hydra bosses), new NPCs and quest arcs, the death-and-respawn "deathscape" flavour.
- **Phase 8: Book 3 (Evolution):** ZONES_BOOK3 (the isle, the temple floors, late-game bosses), the evolution mechanics, late-game systems (city-building, mounts, pets as far as ACE allows).
- **Phase 9: Systems tail:** affinity as a title/reputation line, guild-style "divisions", the leaderboard/stat cards (existing mod source), final balance and the whole-story playthrough.

## 4. Build method and who does the work
Follows the standing orchestration rules: Moss designs and reviews; cheap workers do the hands-on work.
- Sonnet subagents write SQL/mod source from written specs (one bounded piece each, effort scaled to the piece).
- The converter generates most Book 1 data from the game's JSON, which is cheaper than writing SQL by hand.
- Haiku `verifier` runs every check: `ac_apply.py check` (SQL lint + trial import), mod compile checks, tests, log reads.
- Nothing is applied to the VPS in any phase without Tom.

## 5. Risks
| # | Risk | Mitigation |
|---|---|---|
| R1 | Copyright of the books' characters and text | D1 default: original names and lines, story beats only |
| R2 | Necromancy is hard to fake: no new spells or models | Phase 0 spike C decides pets vs. generators; items and existing spells fill the rest; be explicit about approximations |
| R3 | Headset memory in busy areas | keep object counts low, stage sets away from towns, per-phase perf check |
| R4 | Untested mods (all proposed mods are untested in game) | Phase 0 spike E tests the ones the story needs before relying on them |
| R5 | Scope: three books | ship per book; Book 1 is the whole of Phases 0-6; Books 2 and 3 are re-planned after Book 1 is playable |

## 6. Suggested first steps (in order)
1. Tom answers D1 (names) - or accepts the default.
2. Run Phase 0 spikes A-E on the local stack (verifier agents, no VPS).
3. Review the feasibility report, then start Phase 1.
