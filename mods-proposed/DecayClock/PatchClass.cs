using ACE.Common;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace DecayClock;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object Gate = new();

    private class Track
    {
        public double DecaySeconds;
        public DateTime Last = DateTime.UtcNow;
        public int Warned;
        public bool AtNexus;
    }
    // Memory only: lost on restart.
    private static readonly Dictionary<uint, Track> Tracks = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer?.Dispose();
        var secs = Math.Max(10, Cfg.SweepSeconds);
        timer = new Timer(_ => Sweep(), null, secs * 1000, secs * 1000);
        ModManager.Log("[DecayClock] ready: /decay" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        lock (Gate) Tracks.Clear();
        base.Stop();
    }

    private static bool Ours(CombatPet p, Settings cfg) =>
        !p.IsDestroyed && p.PetOwner is { } o && o != 0 && new ACE.Entity.ObjectGuid(o).IsPlayer() && cfg.MinionWcids.Contains(p.WeenieClassId);

    private static bool InNexus(ACE.Entity.Position? pos, NexusSettings n)
    {
        if (n.Cell == 0 || pos == null) return false;
        if ((pos.Cell >> 16) != (n.Cell >> 16)) return false;
        double dx = pos.PositionX - n.X, dy = pos.PositionY - n.Y, dz = pos.PositionZ - n.Z;
        return dx * dx + dy * dy + dz * dz <= (double)n.Radius * n.Radius;
    }

    private static bool Daylight(Settings cfg)
    {
        if (cfg.DaylightMultiplier == 1.0) return false;
        try { return Timers.CurrentInGameTime.IsDaytime; } catch { return false; }
    }

    private static List<CombatPet> FindPets(Settings cfg)
    {
        var list = new List<CombatPet>();
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                if (wo is CombatPet pet && Ours(pet, cfg)) list.Add(pet);
        return list;
    }

    private static void Chat(Player? p, string msg)
    {
        if (p?.Session != null) p.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
    }

    private static void Sweep()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        try
        {
            var now = DateTime.UtcNow;
            var total = Math.Max(1.0, cfg.DecayMinutesToDeath) * 60.0;
            var day = Daylight(cfg);
            var seen = new HashSet<uint>();
            foreach (var pet in FindPets(cfg))
            {
                var owner = PlayerManager.GetOnlinePlayer(pet.PetOwner!.Value);
                if (owner == null) continue; // offline owner: MinionCleanup's job; no decay meanwhile
                var guid = pet.Guid.Full;
                seen.Add(guid);
                Track t;
                lock (Gate)
                {
                    if (!Tracks.TryGetValue(guid, out t!)) Tracks[guid] = t = new Track { Last = now };
                    var dt = Math.Min((now - t.Last).TotalSeconds, cfg.SweepSeconds * 3.0);
                    t.Last = now;
                    t.AtNexus = InNexus(pet.Location, cfg.Nexus);
                    if (!t.AtNexus) t.DecaySeconds += dt * (day ? cfg.DaylightMultiplier : 1.0);
                }
                var frac = Math.Min(1.0, t.DecaySeconds / total);
                var pct = (int)(frac * 100);
                var p = pet;
                var o = owner;
                new ActionChain(p, () =>
                {
                    if (p.IsDestroyed) return;
                    if (frac >= 1.0)
                    {
                        Chat(o, "Your " + p.Name + " crumbles to dust.");
                        p.Destroy();
                        return;
                    }
                    // Direct vital write, no damage event, so no combat side effects.
                    var cap = (int)Math.Max(1, Math.Ceiling(p.Health.MaxValue * (1.0 - frac)));
                    if (p.Health.Current > cap) p.UpdateVital(p.Health, cap);
                }).EnqueueChain();
                foreach (var w in cfg.WarnAtPercent.OrderBy(x => x))
                {
                    if (pct < w || t.Warned >= w) continue;
                    t.Warned = w;
                    Chat(owner, w >= 95
                        ? "Your " + pet.Name + " is crumbling away."
                        : "Your " + pet.Name + " is decaying (" + (100 - w) + "% of its life left). Bring it back to the nexus.");
                }
            }
            lock (Gate)
                foreach (var k in Tracks.Keys.Where(k => !seen.Contains(k)).ToList()) Tracks.Remove(k);
        }
        catch (Exception ex) { ModManager.Log("[DecayClock] sweep failed: " + ex.Message); }
    }

    [CommandHandler("decay", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows how much life your minions have left.")]
    public static void HandleDecay(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Chat(session.Player, "DecayClock is not enabled."); return; }
        var total = Math.Max(1.0, cfg.DecayMinutesToDeath) * 60.0;
        var mine = session.Player.Guid.Full;
        var n = 0;
        foreach (var pet in FindPets(cfg).Where(p => p.PetOwner == mine))
        {
            Track? t;
            lock (Gate) Tracks.TryGetValue(pet.Guid.Full, out t);
            var left = Math.Max(0, total - (t?.DecaySeconds ?? 0));
            Chat(session.Player, pet.Name + ": about " + (left / 60).ToString("0.#") + " min of life left" + (t?.AtNexus == true ? " (safe at the nexus)." : "."));
            n++;
        }
        if (n == 0) Chat(session.Player, "You have no decaying minions.");
    }
}
