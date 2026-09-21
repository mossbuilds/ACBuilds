# BuffBot (proposed, not deployed)

Player command `/buffme`: casts a configured list of self buffs on the caller, then a cooldown (default 1800 s, per player, resets on restart). Free, no mana or components.

Conservative by design: **off by default** (`Enabled=false` in Settings.json, so the command just says it is switched off), level 6 self buffs only (normal time-limited enchantments, nothing permanent), cooldown, no world objects added.

Settings.json: `Enabled`, `CooldownSeconds`, `Spells` (list of `SpellId` enum names).

Verified against ACE source: `WorldObject.TryCastSpell(Spell, WorldObject, ..., tryResist)` (WorldObject_Magic.cs, namespace ACE.Server.WorldObjects), `Spell(SpellId, bool)` ctor (ACE.Server.Entity), `SpellId` enum (ACE.Entity.Enum, names StrengthSelf6 etc. present). Command name `buffme` not a built-in.

Risks: balance (free buffs); a bad enum name is skipped silently. Test: set Enabled true, `/buffme` twice. Enable later by copying to mods/.
