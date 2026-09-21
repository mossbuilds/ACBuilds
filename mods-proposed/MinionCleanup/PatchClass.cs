using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace MinionCleanup;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer?.Dispose();
        var secs = Math.Max(5, Cfg.SweepSeconds);
        timer = new Timer(_ => Sweep(false), null, secs * 1000, secs * 1000);
        ModManager.Log("[MinionCleanup] ready: /minionsweep" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
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

    /// <summary>Destroys the matching minions. forOwner: only that owner's; else everything failing the checks. Returns (checked, destroyed).</summary>
    private static (int, int) Sweep(bool force, uint forOwner = 0, bool all = false)
    {
        var cfg = Cfg;
        if (cfg == null || (!cfg.Enabled && !force)) return (0, 0);
        int seen = 0, gone = 0;
        try
        {
            foreach (var lb in LandblockManager.GetLoadedLandblocks())
            {
                foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                {
                    if (wo is not CombatPet pet || !Ours(pet, cfg)) continue;
                    var owner = pet.PetOwner!.Value;
                    if (forOwner != 0 && owner != forOwner) continue;
                    seen++;
                    var op = PlayerManager.GetOnlinePlayer(owner);
                    var bad = all || op == null || op.IsLoggingOut || op.Location == null || pet.Location == null
                        || (cfg.OnDeath && (op.IsDead || op.IsInDeathProcess))
                        || (cfg.OnTeleport && (op.Location.Landblock != pet.Location.Landblock || op.Location.DistanceTo(pet.Location) > cfg.MaxDistance));
                    if (!bad) continue;
                    gone++;
                    var p = pet;
                    new ActionChain(p, () => { if (!p.IsDestroyed) p.Destroy(); }).EnqueueChain();
                }
            }
        }
        catch (Exception ex) { ModManager.Log("[MinionCleanup] sweep failed: " + ex.Message); }
        return (seen, gone);
    }

    // Logout: ACE destroys only CurrentActivePet, so extra minions would be left behind.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.LogOut_Inner), new[] { typeof(bool) })]
    public static void PreLogout(Player __instance)
    {
        if (Cfg is { Enabled: true, OnLogout: true }) Sweep(false, __instance.Guid.Full, true);
    }

    // Player.Die is protected: patched by name with explicit ArgumentTypes (DamageHistoryInfo, ACE.Server.Entity).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Die", new[] { typeof(DamageHistoryInfo), typeof(DamageHistoryInfo) })]
    public static void PostDie(Player __instance)
    {
        if (Cfg is { Enabled: true, OnDeath: true }) Sweep(false, __instance.Guid.Full, true);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.Teleport), new[] { typeof(ACE.Entity.Position), typeof(bool) })]
    public static void PostTeleport(Player __instance)
    {
        if (Cfg is not { Enabled: true, OnTeleport: true }) return;
        var owner = __instance.Guid.Full;
        // Position is set by now; run the check a moment later on the timer thread, actual destroys are queued.
        var t = new Timer(_ => Sweep(false, owner), null, 3000, Timeout.Infinite);
        _ = t;
    }

    [CommandHandler("minionsweep", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Runs a MinionCleanup sweep now and prints how many minions were checked and removed.")]
    public static void HandleSweep(Session session, params string[] parameters)
    {
        var (seen, gone) = Sweep(true);
        var msg = $"MinionCleanup: {seen} minion(s) checked, {gone} removed.";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
