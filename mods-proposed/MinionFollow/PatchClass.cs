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
        var secs = Math.Max(0.25, Cfg.CheckSeconds);
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
                    if (pet.IsDead || pet.AttackTarget != null || pet.IsMoving) continue; // in combat / already moving: leave ACE's own AI alone

                    var owner = PlayerManager.GetOnlinePlayer(pet.PetOwner!.Value);
                    if (owner == null || owner.Location == null || pet.Location == null) continue;

                    var dist = owner.Location.DistanceTo(pet.Location);
                    if (dist <= cfg.MinDistance) continue;

                    if (dist > cfg.MaxDistance) continue; // MinionCleanup's job to remove an abandoned minion, not ours to teleport it

                    var p = pet;
                    var o = owner;
                    new ActionChain(p, () =>
                    {
                        if (p.IsDestroyed || p.IsDead || p.AttackTarget != null || p.IsMoving) return;
                        if (o.IsDestroyed || o.Location == null) return;

                        // Same two calls Pet.StartFollow makes for a passive pet: broadcast the move-to-object motion,
                        // then drive it server-side through the physics object (CombatPet has no follow logic of its own).
                        p.IsMoving = true;
                        p.MoveTo(o, p.GetRunRate());

                        var mvp = new MovementParameters();
                        mvp.DistanceToObject = cfg.MinDistance;
                        mvp.WalkRunThreshold = 0.0f;
                        p.PhysicsObj?.MoveToObject(o.PhysicsObj, mvp);
                    }).EnqueueChain();
                }
            }
        }
        catch (Exception ex) { ModManager.Log("[MinionFollow] sweep failed: " + ex.Message); }
    }
}
