using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace HotspotAlert;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly Dictionary<uint, DateTime> lastAlert = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var every = TimeSpan.FromSeconds(Math.Max(30, Cfg.ScanSeconds));
            timer = new Timer(_ =>
            {
                try { Scan(); }
                catch (Exception e) { ModManager.Log($"[HotspotAlert] {e.Message}"); }
            }, null, every, every);
        }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    // Counts creatures and players in one landblock. GetAllWorldObjectsForDiagnostics() is a snapshot list built for cross-thread reads.
    private static (int players, int creatures) Count(Landblock lb)
    {
        int players = 0, creatures = 0;
        foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
        {
            if (wo is Player) players++;
            else if (wo is Creature) creatures++;
        }
        return (players, creatures);
    }

    private static void Scan()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromMinutes(Math.Max(1, cfg.AlertCooldownMinutes));
        var lines = new List<string>();
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            var (players, creatures) = Count(lb);
            if (creatures < cfg.CreatureThreshold && players < cfg.PlayerThreshold) continue;
            var id = lb.Id.Raw;
            if (lastAlert.TryGetValue(id, out var t) && now - t < cooldown) continue;
            lastAlert[id] = now;
            lines.Add($"Hotspot {id >> 16:X4}: {players} players, {creatures} creatures.");
        }
        if (lines.Count == 0) return;
        foreach (var p in PlayerManager.GetAllOnline())
        {
            if (p.Session == null || p.Session.AccessLevel < AccessLevel.Sentinel) continue;
            var admin = p;
            new ActionChain(admin, () =>
            {
                foreach (var l in lines)
                    admin.Session?.Network.EnqueueSend(new GameMessageSystemChat(l, ChatMessageType.Broadcast));
            }).EnqueueChain();
        }
    }

    [CommandHandler("hotspots", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0, "Lists landblocks over the hotspot thresholds right now.", "")]
    public static void HandleHotspots(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        int shown = 0;
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            var (players, creatures) = Count(lb);
            if (creatures < cfg.CreatureThreshold && players < cfg.PlayerThreshold) continue;
            shown++;
            var msg = $"Hotspot {lb.Id.Raw >> 16:X4}: {players} players, {creatures} creatures.";
            if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            else Console.WriteLine(msg);
        }
        if (shown == 0)
        {
            var none = $"No landblock is over {cfg.CreatureThreshold} creatures or {cfg.PlayerThreshold} players.";
            if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(none, ChatMessageType.Broadcast));
            else Console.WriteLine(none);
        }
    }
}
