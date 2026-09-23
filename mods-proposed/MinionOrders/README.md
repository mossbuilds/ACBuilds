# MinionOrders
Source only, not compiled or deployed. Squad orders for RaiseSkeleton minions (CombatPet, weenie ids in MinionWcids, default 900021240), only those whose PetOwner is the caller. OFF by default.
Command: /order attack | hold | follow (Player, RequiresWorld; "order" is not among the 327 built-in names). Per-player cooldown OrderCooldownMs.
Implemented: hold = state in a ConditionalWeakTable, AttackTarget=null, Creature.CancelMoveTo(), and Harmony prefixes on CombatPet.HandleFindTarget/FindNextTarget stop target search; attack = sets AttackTarget to the caller's selected creature (Player.HealthQueryTarget, resolved with Landblock.GetObject) if Creature.CanDamage allows (never players), pinned by the same prefixes until it dies, then stock AI; follow = clears the state, stock AI (auto-target) resumes. All pet changes run on the pet via ActionChain.
Not done: /order recall and an active follow-the-owner. Stock CombatPet is not passive, so it has no follow logic (Pet.StartFollow is private and only used by passive pets); minions only chase targets, so "follow" means "resume stock behaviour" and "hold" means stand still, not heel.
Settings.json: Enabled false, MinionWcids [900021240], OrderCooldownMs 1500. Unverified: HealthQueryTarget tracks selection only after the client queries health; ActionChain and BasicPatch usage not compiled; Pet.PetOwner (uint?) assumed as in MinionCleanup.

## Spell binding
`/order attack|hold|follow` is also three real spells: `Settings.AttackSpellId` / `HoldSpellId` / `FollowSpellId`
(all `0` = unbound by default). Casting one runs `GiveOrder` instead of the spell's stock effect - mana,
components, cast animation, fizzle chance and skill gain all happen normally, via the same
`WorldObject.HandleCastSpell` bind point RaiseSkeleton uses (only runs after the cast already succeeded). Only
a player's own direct cast is claimed; everything else falls through untouched.

For "attack", the cast's own `target` is used when it resolves to a hostile `Creature` (not dead, not a
`Player`, `CanDamage` allows it); otherwise it falls back to the caller's current selection
(`Player.HealthQueryTarget`), exactly like `/order attack` does when you have no spell target selected.

Gated by `Settings.RequirePath` (default `"necromancer"`, same `PathChoice` quest-stamp check as
RaiseSkeleton; empty = no gate). A non-necromancer casting a bound spell gets "Only a necromancer can shape
this magic." and the order is never given.

Recommended spell ids, all currently unused (usage 0):
- `AttackSpellId`: **5332 "Bael'zharon's Nether Streak"**
- `HoldSpellId`: **4194 "Magical Void"**
- `FollowSpellId`: **3235 "Dark Power"**

**Ship all three settings at 0.** Confirm each with `/spellinfo <id>` (`player-castable: yes`), then
`/addspell <id>` and cast it in game before switching any of them on.
