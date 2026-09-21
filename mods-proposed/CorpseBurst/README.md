# CorpseBurst (proposed, off by default)

`/burst` destroys the nearest lootable monster corpse (same checks as RaiseSkeleton) and casts one existing projectile spell (Settings.SpellId, default FlameRing) via `WorldObject.TryCastSpell(Spell, target, tryResist:false)`, no components or spellbook needed.

Honest limits: ACE fires spell projectiles from the caster, so the burst comes out of the player, not the corpse; the corpse only gates the command and is consumed. Target is the living monster nearest the corpse (BurstRadius). Damage is only what the spell does; corpse max health does not scale it. No per-cast cap on affected creatures (projectile count is fixed by the spell). Players are hit only if ACE's normal spell/PK rules allow. Cooldown per player, optional ManaCost via UpdateVitalDelta. Does not touch the databases. Not compiled or tested.
