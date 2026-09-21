using ACE.Database;
using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace WorldBoss;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const uint CoinWcid = 273; // pyreal coin stack
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static WorldObject? boss;      // the single live boss
    private static string bossName = "";
    private static DateTime spawnedAt;
    private static bool spawning;          // set while a spawn is queued: never two bosses
    private static string lastSchedKey = "";

    private static void Say(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Reply(Session? s, string msg)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else ModManager.Log($"[WorldBoss] {msg}");
    }

    // Creature.Die(DamageHistoryInfo lastDamager, DamageHistoryInfo topDamager) is protected virtual (Creature_Death.cs).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), "Die", new[] { typeof(DamageHistoryInfo), typeof(DamageHistoryInfo) })]
    public static void PostDie(Creature __instance, DamageHistoryInfo topDamager)
    {
        try
        {
            lock (gate) { if (boss == null || !ReferenceEquals(__instance, boss)) return; boss = null; }
            var cfg = Cfg;
            var who = topDamager?.Name ?? "unknown";
            Say(string.Format(cfg?.DeathMessage ?? "{0} slain, top: {1}", bossName, who));
            if (cfg != null && cfg.RewardPyreals > 0 && topDamager != null && topDamager.IsPlayer)
            {
                var p = PlayerManager.GetOnlinePlayer(topDamager.Guid);
                if (p != null)
                    new ActionChain(p, () =>
                    {
                        var coin = WorldObjectFactory.CreateNewWorldObject(CoinWcid);
                        if (coin == null) return;
                        coin.SetStackSize(cfg.RewardPyreals);
                        p.TryCreateInInventoryWithNetworking(coin);
                    }).EnqueueChain();
            }
        }
        catch (Exception e) { ModManager.Log($"[WorldBoss] {e.Message}"); }
    }

    private static Position? ConfiguredPosition(Settings c)
    {
        try
        {
            var cell = Convert.ToUInt32(c.Cell.Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16);
            if (cell == 0) return null;
            return new Position(cell, c.X, c.Y, c.Z, 0, 0, 0, 1);
        }
        catch { return null; }
    }

    private static void Start(Session? s, uint wcid, Player? nearPlayer)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(s, "WorldBoss is disabled in settings."); return; }
        lock (gate)
        {
            if (boss != null || spawning) { Reply(s, "A boss is already active."); return; }
            spawning = true;
        }
        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
        if (weenie == null) { spawning = false; Reply(s, $"Boss wcid {wcid} not found."); return; }
        Position? pos = nearPlayer == null ? ConfiguredPosition(cfg) : null;
        if (nearPlayer == null && pos == null) { spawning = false; Reply(s, "No valid Cell/X/Y/Z configured."); return; }

        void Spawn()
        {
            try
            {
                var obj = WorldObjectFactory.CreateNewWorldObject(weenie);
                if (obj == null) { spawning = false; return; }
                obj.Location = nearPlayer != null ? nearPlayer.Location.InFrontOf(5f, true) : new Position(pos!);
                obj.Location.LandblockId = new LandblockId(obj.Location.GetCell());
                if (obj.EnterWorld())
                {
                    lock (gate) { boss = obj; bossName = obj.Name; spawnedAt = DateTime.UtcNow; spawning = false; }
                    Say(string.Format(cfg.StartMessage, obj.Name));
                    ModManager.Log($"[WorldBoss] spawned {obj.Name} at {obj.Location.ToLOCString()}");
                }
                else { spawning = false; Reply(s, "Boss could not enter the world (blocked position?)."); }
            }
            catch (Exception e) { spawning = false; ModManager.Log($"[WorldBoss] {e.Message}"); }
        }

        if (nearPlayer != null) new ActionChain(nearPlayer, Spawn).EnqueueChain();
        else new ActionChain(WorldManager.ActionQueue, Spawn).EnqueueChain();
    }

    private static void Despawn(bool announce)
    {
        WorldObject? wo;
        lock (gate) { wo = boss; boss = null; }
        if (wo == null) return;
        new ActionChain(WorldManager.ActionQueue, () => wo.Destroy()).EnqueueChain();
        if (announce) Say(string.Format(Cfg?.DespawnMessage ?? "{0} vanished", bossName));
    }

    [CommandHandler("worldboss", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Manage the world boss event.", "start [wcid] | stop | status")]
    public static void HandleWorldBoss(Session session, params string[] p)
    {
        var cfg = Cfg;
        switch (p[0].ToLowerInvariant())
        {
            case "start":
                var wcid = cfg?.BossWcid ?? 900021232;
                if (p.Length > 1 && !uint.TryParse(p[1], out wcid)) { Reply(session, "Bad wcid."); return; }
                Start(session, wcid, cfg != null && cfg.SpawnNextToAdmin ? session?.Player : null);
                break;
            case "stop":
                bool had; lock (gate) had = boss != null;
                if (!had) { Reply(session, "No boss active."); return; }
                Despawn(true); Reply(session, "Boss removed.");
                break;
            default:
                lock (gate)
                    Reply(session, boss == null ? "No boss active." : $"{bossName} active for {(int)(DateTime.UtcNow - spawnedAt).TotalMinutes} min.");
                break;
        }
    }

    private static void Tick()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        bool active; DateTime at;
        lock (gate) { active = boss != null; at = spawnedAt; }
        if (active && (DateTime.UtcNow - at).TotalMinutes >= cfg.MaxMinutes) { Despawn(true); return; }
        if (active || !cfg.ScheduleEnabled) return;
        var now = DateTime.Now;
        var key = now.ToString("yyyyMMdd") + cfg.Time;
        if (now.ToString("HH:mm") != cfg.Time || key == lastSchedKey) return;
        var days = cfg.Days.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!days.Any(d => now.DayOfWeek.ToString().StartsWith(d, StringComparison.OrdinalIgnoreCase))) return;
        lastSchedKey = key;
        Start(null, cfg.BossWcid, null);
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[WorldBoss] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }
}
