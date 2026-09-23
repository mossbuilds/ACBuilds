# RaiseSkeleton

`/raiseskel` consumes the nearest monster corpse and raises a skeleton. Up to your control limit it is a real ACE combat pet that follows and fights for you; past the limit it is a feral, hostile skeleton. Nothing is deployed and nothing was compiled or run yet.

## Commands (any player)
- `/raiseskel` - nearest monster corpse within `Radius` on your landblock that you may loot (`Corpse.HasPermission`); player corpses are never touched (`Corpse.IsMonster` and a non-player `VictimId` are both required). The corpse and anything still inside it is destroyed.
- `/minions` - count/limit and each minion's health.
- `/dismiss` - destroys all your controlled minions (no corpse comes back).

## Control limit
`limit = floor(Self.Current / Divisor) + BonusLimit`, clamped to `0..MaxMinionsHardCap`. ACE has no Willpower; the Self attribute stands in. Over the limit, `/raiseskel` still eats the corpse and spawns the feral skeleton next to you.

## How the minion is made
The exact retail summon path (`PetDevice.SummonCreature`): `WorldObjectFactory.CreateNewWorldObject(MinionWcid)` gives a `CombatPet`, then `CombatPet.Init(player, petDevice)` (-> `Pet.Init`) sets the owner (`PetOwner`, `P_PetOwner`), prefixes the owner's name, places it in front of the caller and calls `EnterWorld`; CombatPet then runs its own target-finding and melee AI. `Init` needs a PetDevice object, so the mod creates a transient retail one (`DeviceWcid`, 48942, never enters the world). ACE allows only one active pet (`Player.CurrentActivePet`); the mod clears that field around `Init` so several minions can exist and restores a real pet afterwards. The pet removes itself from `CurrentActivePet` when destroyed.

## SQL (sql/, not applied)
- `900021240_raiseskelminion.sql`, class `raiseskelminion`: clone of retail CombatPet weenie 48943 ("Skeleton", level 50, WeenieType CombatPet) renamed "Skeletal Minion", with the retail `Lifespan` row removed (the mod owns the lifetime).
- `900021241_raiseskelferal.sql`, class `raiseskelferal`: clone of retail creature 1762 (Skeleton Lord, level 40) renamed "Feral Skeleton"; death treasure and create list removed so raise-kill-raise cannot farm loot.
Apply both with the ac-creator tool before setting `Enabled` true; `DeviceWcid` 48942 must exist in the world DB (retail data).

## Settings.json
`Enabled` (false), `MinionWcid` 900021240, `FeralWcid` 900021241, `DeviceWcid` 48942, `Radius` 6, `Divisor` 10, `BonusLimit` 0, `MaxMinionsHardCap` 10, `ManaCost` 0 (spent with `UpdateVitalDelta(Mana, -cost)`, as Player_Magic does), `CooldownSeconds` 2, `MinionMinutes` 30 (0 = never), `MinCorpseLevel` 0 (a corpse with no Level is refused when above 0).

## What is approximated (honest list)
- No new models: both skeletons reuse existing retail skeleton models/palettes, so a raised "wolf" is a skeleton.
- `Self` stands in for Willpower.
- Level scaling: none. The minion is a fixed level-50 retail skeleton, the feral one fixed level 40 (feral is slightly weaker than a controlled minion). Setting level or vitals per corpse safely was not attempted; the corpse's level only gates via `MinCorpseLevel`.
- Headset-memory cap: the minion lists live in memory only, so a restart forgets them (the live pets are world objects and go too), a logged-out owner's minions are not tracked, and the count is capped by `MaxMinionsHardCap` to keep the world's monster load bounded. Minions despawn after `MinionMinutes`; each minion is a full AI creature, so keep the cap modest.
- Feral skeletons keep retail XP value and can be killed for XP, and their corpses can be raised again.

## Unverified
Not compiled. Not tested in game: CombatPet AI with several pets of one owner (ACE assumes one), whether `Corpse.Level` is ever set, and that the retail level-50 skeleton pet is not too strong for your economy.

## Spell binding
`/raiseskel` and `/dismiss` are also real spells: `Settings.RaiseSpellId` / `DismissSpellId` (both `0` = unbound
by default). Casting either spell runs `RaiseFromNearestCorpse` / `DismissAll` instead of the spell's stock
effect - mana, components, cast animation, fizzle chance and skill gain all happen normally, since the bind
point (`WorldObject.HandleCastSpell`, ACE master `WorldObjects/WorldObject_Magic.cs` line 259) only runs after
the cast has already succeeded (Player_Magic.cs already spent components/mana and rolled fizzle). The Harmony
prefix only claims a cast when it is a player's own direct cast (`itemCaster == null`, `!fromProc`, `!equip`)
of one of these two ids; every other cast (monsters, items, procs, other mods' ids, id `0`) falls through to
stock behaviour untouched.

Gated by `Settings.RequirePath` (default `"necromancer"`, checked with
`player.QuestManager.HasQuest("path_" + RequirePath)`, the same stamp `PathChoice` writes with
`/path choose necromancer`; empty string = no gate). A non-necromancer casting a bound spell gets "Only a
necromancer can shape this magic.", the ability does not run, and the stock spell effect is also skipped (the
mana/components are still spent - the cast already succeeded upstream).

Recommended spell ids, both currently unused (usage 0 in `docs/necromancer-spells-data/necro_candidates_usage.tsv`):
- `RaiseSpellId`: **3801 "Shadow Touch"** - dark-touch flavored, thematically fits a raise.
- `DismissSpellId`: **3803 "Shadow Shot"** - distinct id, same Void-family flavor.

**Ship both settings at 0.** Before flipping either on: `/spellinfo 3801` and `/spellinfo 3803` (SpellInfo
mod) to confirm `player-castable: yes`, then `/addspell 3801` / `/addspell 3803` on a test character and cast
each from the spellbook to confirm it actually fires `RaiseFromNearestCorpse`/`DismissAll` before relying on
it live.
