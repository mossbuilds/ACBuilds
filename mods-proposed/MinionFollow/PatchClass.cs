using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace MinionFollow;

/// <summary>
/// ACE's own idle-follow logic (Pet.SlowTick -> StartFollow) only runs for PASSIVE pets: Monster_Tick.cs checks
/// `IsPassivePet` before calling it. A CombatPet (like RaiseSkeleton's skeleton minion) with no AttackTarget instead
/// hits Monster_Tick's `Sleep()` path and just stands still - it will only move once something is worth fighting.
/// This mod reuses the same movement calls Pet.StartFollow uses (Creature.MoveTo + PhysicsObj.MoveToObject) so a
/// CombatPet walks back to its owner when idle and too far away, on a periodic sweep.
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer?.Dispose();
        // ACE's own Pet.Tick (the thing that actually progresses a passive pet's walk) runs 5x/second and every
        // cycle calls PhysicsObj.update_object() + UpdatePosition_SyncLocation() + SendUpdatePosition() - a single
        // MoveToObject call does nothing more than start the client's run animation; without repeating those three
        // calls every cycle the server-side position never advances, so the minion looks like it's running in
        // place. CheckSeconds therefore needs to be this fast, not a once-a-second decision poll.
        var secs = Math.Max(0.1, Cfg.CheckSeconds);
        timer = new Timer(_ => Sweep(), null, (int)(secs * 1000), (int)(secs * 1000));
        ModManager.Log("[MinionFollow] ready" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    private static bool Ours(CombatPet p, Settings cfg) =>
        !p.IsDestroyed && p.PetOwner is { } o && o != 0 && new ACE.Entity.ObjectGuid(o).IsPlayer() && cfg.MinionWcids.Contains(p.WeenieClassId);

    private static void Sweep()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        try
        {
            foreach (var lb in LandblockManager.GetLoadedLandblocks())
            {
                foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                {
                    if (wo is not CombatPet pet || !Ours(pet, cfg)) continue;
                    if (pet.IsDead || pet.AttackTarget != null) continue; // in combat: leave ACE's own AI alone entirely

                    var owner = PlayerManager.GetOnlinePlayer(pet.PetOwner!.Value);
                    if (owner == null || owner.Location == null || pet.Location == null) continue;

                    var dist = owner.Location.DistanceTo(pet.Location);
                    var p = pet;
                    var o = owner;

                    if (p.IsMoving)
                    {
                        // Already following: this is the part a single MoveToObject call does NOT do on its own.
                        // Mirrors Pet.Tick exactly (the only place ACE calls these three together for a walking pet).
                        new ActionChain(p, () =>
                        {
                            if (p.IsDestroyed || p.PhysicsObj == null) { p.IsMoving = false; return; }
                            p.PhysicsObj.update_object();
                            p.UpdatePosition_SyncLocation();
                            p.SendUpdatePosition();
                            if (p.AttackTarget != null || o.IsDestroyed || o.Location == null
                                || o.Location.DistanceTo(p.Location) <= cfg.MinDistance)
                                p.IsMoving = false; // arrived, or something else took over - stop progressing it here
                        }).EnqueueChain();
                        continue;
                    }

                    if (dist <= cfg.MinDistance) continue;
                    if (dist > cfg.MaxDistance) continue; // MinionCleanup's job to remove an abandoned minion, not ours to teleport it

                    new ActionChain(p, () =>
                    {
                        if (p.IsDestroyed || p.IsDead || p.AttackTarget != null || p.IsMoving) return;
                        if (o.IsDestroyed || o.Location == null || p.PhysicsObj == null) return;

                        // Same calls Pet.StartFollow makes for a passive pet: broadcast the move-to-object motion,
                        // kick off the physics-level walk, then run one progress step immediately so it doesn't
                        // wait a full cycle before the server-side position starts advancing (see IsMoving branch above).
                        p.IsMoving = true;
                        p.MoveTo(o, p.GetRunRate());

                        var mvp = new MovementParameters();
                        mvp.DistanceToObject = cfg.MinDistance;
                        mvp.WalkRunThreshold = 0.0f;
                        p.PhysicsObj.MoveToObject(o.PhysicsObj, mvp);

                        p.PhysicsObj.update_object();
                        p.UpdatePosition_SyncLocation();
                        p.SendUpdatePosition();
                    }).EnqueueChain();
                }
            }
        }
        catch (Exception ex) { ModManager.Log("[MinionFollow] sweep failed: " + ex.Message); }
    }
}
