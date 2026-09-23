# ThreatTableView (proposed, not deployed, not compiled)

Read-only Sentinel diagnostic for combat-threat disputes ("who actually has kill credit / why did this
mob switch targets") - asked *before* the kill, unlike the already-shipped `LootWatch` (idea 45), which
only audits *after* a corpse is opened.

## What it does

`/threattable` (Sentinel+, no arguments) reads the caller's last-appraised (examined) or last-found
object via the standard `Player.RequestedAppraisalTarget` / `Player.FindObject` lookup (same pattern as
`PriceCheck`/`VendorStock`/`TinkerHistory`/`GeneratorNudge`). If that object is a `Creature`, it prints:

- current `AttackTarget` (who the monster is currently attacking)
- configured `TargetingTactic` and the live `CurrentTargetingTactic` actually in effect
- `VisualAwarenessRange` / `AuralAwarenessRange` (in world units, derived back out of the cached
  squared values, live-scaled by the `mob_awareness_range` server property)
- `TotalHealth` tracked in `DamageHistory` (sum of all damager contributions currently on record)
- the ranked damager list (`DamageHistory.Damagers`, sorted by damage descending, capped at
  `Settings.MaxDamagers`), each with name, GUID, player/non-player flag, and total damage
- `TopDamager` (who currently gets corpse-looting/"killed by" credit) and `LastDamager`

## What it does NOT do

- Never calls anything that mutates combat state, aggro, or the damage log - every member read is a
  plain public getter ACE itself already exposes for its own corpse/looting/targeting logic.
- Never resolves `DamageHistoryInfo.Attacker`'s `WeakReference<WorldObject>` via `TryGetAttacker()` -
  the plain `Name`/`Guid`/`TotalDamage`/`IsPlayer` fields captured at construction time are enough, so
  a long-dead attacker (already garbage collected) still prints correctly instead of risking a null.
- Does not work on players, only `Creature` targets (a monster's threat table is the point of the idea;
  a player has no `DamageHistory`-driven targeting AI to diagnose).
- No settings beyond `Enabled` and `MaxDamagers` - no patches, no state written anywhere.

## Verified this round (ACEmulator/ACE master, full-file fetch via github-second-brain)

- `Creature.DamageHistory` - `public DamageHistory DamageHistory { get; private set; }`
  (`Source/ACE.Server/WorldObjects/Creature_Combat.cs`)
- `DamageHistory.Damagers` / `TopDamager` / `LastDamager` / `TotalHealth` - all `public`
  (`Source/ACE.Server/Entity/DamageHistory.cs`)
- `DamageHistoryInfo.Guid` / `Name` / `TotalDamage` / `IsPlayer` - all `public`
  (`Source/ACE.Server/Entity/DamageHistoryInfo.cs`)
- `Creature.TargetingTactic` / `CurrentTargetingTactic` / `VisualAwarenessRangeSq` /
  `AuralAwarenessRangeSq` - all `public` (`Source/ACE.Server/WorldObjects/Monster_Awareness.cs`)
- `Creature.AttackTarget` - the idea's own pointer said it is "used pervasively as a public member
  throughout `Monster_Awareness.cs`", but that file only *uses* it, never declares it. This round
  located the actual declaration in `Source/ACE.Server/WorldObjects/Monster_Combat.cs`:
  `public WorldObject AttackTarget;` - plain public field, confirmed further by the fact that
  `Creature_Combat.cs`'s `AlertMonster()` sets it through an unrelated `Creature`-typed reference and
  `Player.cs` (a subclass) sets it through a plain `Creature`-typed variable; C#'s protected-access
  rule would reject the second call if the field were only `protected`, so it must be `public` (or at
  minimum `internal`, and either way readable from a mod in the same assembly).

## Risks

- `DamageHistoryInfo.Attacker` is a `WeakReference<WorldObject>` - flagged in the idea text as a risk,
  but this mod never calls `TryGetAttacker()`/`TryGetPetOwner()`, only the plain fields captured at
  construction, so garbage collection of the underlying object cannot break this command.
- Read-only: cannot desync, corrupt, or change any combat outcome. Worst case is a stale snapshot if
  the target dies/despawns between appraisal and the command running - `FindObject` returning `null`
  is already handled with a plain message.

## How to test

Compile-check only (source-only mod, not deployed): `bash mods-proposed/check-mod.sh ThreatTableView`
must print `MOD OK`. To exercise it for real later: enable in `Settings.json`, appraise a live monster
as a Sentinel+ character, run `/threattable`, and confirm the printed `AttackTarget`/damager list
matches what is actually happening in combat.

## How to enable later

Off by default (`Enabled: false` in `Settings.json`). Flip to `true` and reload/restart to activate;
no other configuration needed.
