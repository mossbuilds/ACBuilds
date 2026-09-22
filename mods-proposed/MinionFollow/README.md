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
A periodic sweep (`CheckSeconds`, default 1s - matching ACE's own passive-pet `SlowTick` cadence) looks at every
`CombatPet` whose weenie class id is in `MinionWcids` (default `[900021240]`, RaiseSkeleton's skeleton). For a
minion that is idle (`AttackTarget == null`, not already moving, not dead) and farther than `MinDistance` from its
online owner, it makes the minion walk to the owner using the **same two calls** `Pet.StartFollow` uses for a
passive pet:
- `Creature.MoveTo(owner, GetRunRate())` - broadcasts the move-to-object motion to clients.
- `PhysicsObj.MoveToObject(owner.PhysicsObj, movementParams)` - drives the actual server-side walk.

If the minion is farther than `MaxDistance`, this mod does nothing (leaves it be) rather than teleport it - that is
MinionCleanup's job (it already destroys an abandoned minion past its own `MaxDistance`).

## Settings (Settings.json)
`Enabled` (false), `MinionWcids` ([900021240]), `MinDistance` (4 - matches ACE's own passive-pet gap), `MaxDistance`
(60 - matches MinionCleanup's default so the two agree), `CheckSeconds` (1.0).

## Interactions with other mods
- Does not touch a minion that is already moving or has an `AttackTarget` - it leaves ACE's own combat AI alone.
- Does not coordinate with `MinionOrders`' `/order hold` state (that state is private to MinionOrders). A held
  minion has `AttackTarget == null`, so this mod may walk it back to you even while "held" - if you want hold to
  mean "stay put no matter what," that needs a shared flag between the two mods (not built here).
- `MinionCleanup` still owns destroying an abandoned/offline-owner minion; this mod only ever tries to walk one
  closer, never destroys anything.

## Risks / untested
- Not run in game. The movement calls are copied verbatim from `Pet.StartFollow`, but that method is only ever
  exercised by ACE itself on **passive** pets - using it on a `CombatPet` is new territory. If the client or physics
  layer doesn't like a CombatPet using this path, the walk may look wrong (snapping, stalled, or ignored) even
  though the calls don't throw.
- `Player_Melee.cs`/`Vendor.cs`-style `GetCylinderDistance` was considered for the distance check but its exact
  location/signature could not be pinned down in this pass; `Location.DistanceTo` (already used by MinionCleanup)
  is used instead - fine for a "far enough to bother" check, just not identical to ACE's own follow-distance math.
- No mana/stamina cost; the minion never tires from walking, matching how ACE's own pets behave.

Enable later by copying this folder to `mods/` after `check-mod.sh MinionFollow` prints `MOD OK`.
