# AmbushStrike (proposed, not deployed)
Multiplies a Player's melee/missile damage against non-player creatures that have not noticed them. Postfix on DamageEvent.CalculateDamage (ACE.Server.Entity). Unaware = Creature.AttackTarget == null (first hit, sleeping/idle). RequireBehind uses Creature.GetAngle(WorldObject) (|angle|>90). RequirePathQuest uses QuestManager.HasQuest (e.g. path_rogue from PathChoice).
Settings.json: Enabled(false), Multiplier(2.0, clamp 1-5), RequireBehind(false), OnlyUnaware(true), CooldownSecondsPerTarget(0), RequirePathQuest("").
Command (admin): /ambush [1-5] shows/sets multiplier at runtime. Not compiled or tested; AttackTarget location (Creature partial) and an in-range CombatType enum namespace unverified.
