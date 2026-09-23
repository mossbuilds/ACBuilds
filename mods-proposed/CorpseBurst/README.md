# CorpseBurst (proposed, off by default)

`/burst` destroys the nearest lootable monster corpse (same checks as RaiseSkeleton) and casts one existing projectile spell (Settings.SpellId, default FlameRing) via `WorldObject.TryCastSpell(Spell, target, tryResist:false)`, no components or spellbook needed.

Honest limits: ACE fires spell projectiles from the caster, so the burst comes out of the player, not the corpse; the corpse only gates the command and is consumed. Target is the living monster nearest the corpse (BurstRadius). Damage is only what the spell does; corpse max health does not scale it. No per-cast cap on affected creatures (projectile count is fixed by the spell). Players are hit only if ACE's normal spell/PK rules allow. Cooldown per player, optional ManaCost via UpdateVitalDelta. Does not touch the databases. Not compiled or tested.

## Spell binding
`/burst` is also a real spell: `Settings.BurstSpellId` (`0` = unbound by default). Casting it runs `DoBurst`
instead of the spell's stock effect - mana, components, cast animation, fizzle chance and skill gain all
happen normally, via the same `WorldObject.HandleCastSpell` bind point RaiseSkeleton uses (runs only after the
cast already succeeded; see that README for the full hook detail). `DoBurst` is called directly rather than
through an `ActionChain` since `HandleCastSpell` already runs on the caster's own action queue. Only a
player's own direct cast is claimed; everything else falls through untouched.

Gated by `Settings.RequirePath` (default `"necromancer"`, same `PathChoice` quest-stamp check as
RaiseSkeleton; empty = no gate). A non-necromancer casting a bound spell gets "Only a necromancer can shape
this magic." and the burst never fires.

Recommended id (currently unused, usage 0): **5544 "Nether Blast I"** (Void Magic bolt/AoE family) - the same
pick `docs/NECROMANCER_SPELLS.md` makes for `corpse_explosion`.

**Ship `BurstSpellId` at 0.** Confirm with `/spellinfo 5544` (`player-castable: yes`), then `/addspell 5544`
and cast it in game before switching it on.
