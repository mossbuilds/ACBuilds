# MinionFollow (proposed, not deployed)

Makes RaiseSkeleton's CombatPet minions follow their owner when idle. **Off by default.**

## Why it's needed (verified in ACE master)
`Monster_Tick.cs` only calls `Pet.Tick` (which drives `SlowTick` -> `StartFollow`, the code that makes a summoned
creature walk back to its owner) for **passive** pets:
```
if (IsPassivePet && this is Pet pet) { pet.Tick(currentUnixTime); return; }
```
RaiseSkeleton's minion is a `CombatPet` (non-passive), so it never reaches that path. Instead it falls into ordinary
monster AI (`Monster_Tick.cs`), and with no `AttackTarget` it just calls `Sleep()` and stands still. This matches
real retail CombatPet weenies too - it's an ACE/retail limitation, not something RaiseSkeleton broke.

## What this mod does
A periodic tick (`CheckSeconds`, default 0.2s = 5x/second, matching ACE's own passive-pet `Tick()` cadence) looks at
every `CombatPet` whose weenie class id is in `MinionWcids` (default `[900021240]`, RaiseSkeleton's skeleton) that
isn't currently in combat (`AttackTarget == null`). Two things happen depending on whether the minion is already
following:
- **Not yet following, and farther than `MinDistance` from its online owner:** start it, using the same calls
  `Pet.StartFollow` uses for a passive pet - `Creature.MoveTo(owner, GetRunRate())` (broadcasts the motion to
  clients) then `PhysicsObj.MoveToObject(owner.PhysicsObj, movementParams)` (queues the server-side walk).
- **Already following (`IsMoving == true`):** progress it, every tick, with the exact three calls `Pet.Tick` makes
  for a passive pet - `PhysicsObj.update_object()`, `UpdatePosition_SyncLocation()`, `SendUpdatePosition()`. **This
  part is not optional**: a `MoveToObject` call only starts the client's run animation and queues a physics-layer
  move; nothing advances the server-side position afterward unless something keeps calling `update_object()`. For a
  passive pet that "something" is `Pet.Tick`'s own 5x/second loop; a `CombatPet` never gets that loop while idle
  (`Monster_Tick.cs` bails out to `Sleep()` before reaching any movement-progress code when `AttackTarget == null`).
  Without this half, the minion visibly plays its running animation but never actually moves - the exact bug seen
  the first time this mod was tried live.

If the minion is farther than `MaxDistance`, this mod does nothing (leaves it be) rather than teleport it - that is
MinionCleanup's job (it already destroys an abandoned minion past its own `MaxDistance`).

## Settings (Settings.json)
`Enabled` (false), `MinionWcids` ([900021240]), `MinDistance` (4 - matches ACE's own passive-pet gap), `MaxDistance`
(60 - matches MinionCleanup's default so the two agree), `CheckSeconds` (0.2 - do not slow this down much; see above).

## Interactions with other mods
- Does not touch a minion that is already moving or has an `AttackTarget` - it leaves ACE's own combat AI alone.
- Does not coordinate with `MinionOrders`' `/order hold` state (that state is private to MinionOrders). A held
  minion has `AttackTarget == null`, so this mod may walk it back to you even while "held" - if you want hold to
  mean "stay put no matter what," that needs a shared flag between the two mods (not built here).
- `MinionCleanup` still owns destroying an abandoned/offline-owner minion; this mod only ever tries to walk one
  closer, never destroys anything.

## A second ACE bug this mod has to work around
`CombatPet.FindNextTarget()` (called every tick once a target dies) returns `false` when there is no other nearby
monster to retarget - but it never clears `AttackTarget`. So after a kill, with nothing else around, the pet keeps
a stale reference to the now-dead creature forever. That has two consequences: ACE's own `Monster_Tick` loops
uselessly on `FindNextTarget() -> fail -> return` every tick instead of ever reaching `Sleep()` again, and any mod
(this one included) that checks "is this pet in combat?" via `AttackTarget != null` sees a false positive and
never treats the pet as idle again - which is exactly the "kills a mob, then just stands there forever" symptom
reported live. This mod now clears `AttackTarget` itself whenever it points at a dead creature, before deciding
whether the pet is idle.

## Risks / untested
- **v1 was tried live and failed**: it called `MoveToObject` once per second and never progressed it afterward, so
  the minion just played its running animation in place.
- **v2 was tried live and followed correctly on the initial raise, but stopped following again after its first kill**
  (the dead-`AttackTarget` bug above) - v3 (this version) adds the fix; not yet tried live itself.
- The movement calls are copied verbatim from `Pet.StartFollow`/`Pet.Tick`, but those are only ever exercised by ACE
  itself on **passive** pets - using them on a `CombatPet` is still new territory. Watch for: the minion snapping
  instead of walking smoothly, fighting with its own combat-AI movement the instant it picks up an `AttackTarget`
  mid-walk, or a landblock-crossing edge case in `UpdatePosition_SyncLocation` behaving differently for a CombatPet
  than it does for the passive pets it was written for.
- `Player_Melee.cs`/`Vendor.cs`-style `GetCylinderDistance` was considered for the distance check but its exact
  location/signature could not be pinned down in this pass; `Location.DistanceTo` (already used by MinionCleanup)
  is used instead - fine for a "far enough to bother" check, just not identical to ACE's own follow-distance math.
- No mana/stamina cost; the minion never tires from walking, matching how ACE's own pets behave.
- Runs 5x/second per idle-and-following minion; fine for a handful of minions, would need throttling if a player
  ever has many at once (RaiseSkeleton's own control-limit cap keeps this small in practice).

Enable later by copying this folder to `mods/` after `check-mod.sh MinionFollow` prints `MOD OK`.
