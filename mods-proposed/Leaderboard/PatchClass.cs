using System.Text.Json;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace Leaderboard;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static Dictionary<uint, int> kills = new();
    private static DateTime lastSave = DateTime.MinValue;
    private static bool dirty;
    private static List<(string Name, int Level)>? levelCache;
    private static DateTime cacheAt = DateTime.MinValue;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try { kills = JsonSerializer.Deserialize<Dictionary<uint, int>>(File.ReadAllText(Cfg.DataFile)) ?? new(); } catch { }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        Save();
        base.Stop();
    }

    private static void Save()
    {
        try
        {
            lock (gate)
            {
                if (!dirty || Cfg == null) return;
                File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(kills));
                dirty = false; lastSave = DateTime.UtcNow;
            }
        }
        catch (Exception e) { ModManager.Log($"[Leaderboard] save failed: {e.Message}"); }
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // Creature.OnDeath(DamageHistoryInfo, DamageType, bool) is public virtual in Creature_Death.cs; DamageHistoryInfo is in ACE.Server.Entity.
    // Player overrides it and calls base, so skip Player victims: only monster kills are tallied.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.OnDeath), new Type[] { typeof(DamageHistoryInfo), typeof(DamageType), typeof(bool) })]
    public static void PostOnDeath(Creature __instance, DamageHistoryInfo lastDamager)
    {
        try
        {
            if (Cfg == null || !Cfg.Enabled || __instance is Player || lastDamager == null) return;
            if (lastDamager.TryGetAttacker() is not Player killer) return;
            var doSave = false;
            lock (gate)
            {
                kills[killer.Guid.Full] = kills.GetValueOrDefault(killer.Guid.Full) + 1;
                dirty = true;
                doSave = (DateTime.UtcNow - lastSave).TotalMinutes >= 5;
            }
            if (doSave) Save();
        }
        catch (Exception e) { ModManager.Log($"[Leaderboard] {e.Message}"); }
    }

    // /top is not a built-in ACE command name (grepped [CommandHandler] names).
    [CommandHandler("top", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Show the highest-level characters and top monster killers.", "")]
    public static void HandleTop(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "Leaderboard is switched off."); return; }
        var size = Math.Clamp(cfg.Size, 1, 25);

        List<(string Name, int Level)> levels;
        lock (gate)
        {
            if (levelCache == null || (DateTime.UtcNow - cacheAt).TotalSeconds > cfg.CacheSeconds)
            {
                // PlayerManager.GetAllPlayers() returns List<IPlayer> (in-memory offline + online); IPlayer has Name, Level, Guid.
                levelCache = PlayerManager.GetAllPlayers()
                    .Select(x => (Name: x.Name, Level: x.Level ?? 0))
                    .OrderByDescending(x => x.Level).ThenBy(x => x.Name)
                    .Take(25).ToList();
                cacheAt = DateTime.UtcNow;
            }
            levels = levelCache;
        }
        Say(session, "--- Top levels ---");
        var i = 1;
        foreach (var l in levels.Take(size)) Say(session, $"{i++}. {l.Name} - level {l.Level}");

        List<KeyValuePair<uint, int>> top;
        lock (gate) top = kills.OrderByDescending(k => k.Value).Take(size).ToList();
        Say(session, "--- Top monster killers ---");
        i = 1;
        foreach (var k in top)
        {
            var name = PlayerManager.GetOnlinePlayer(k.Key)?.Name ?? PlayerManager.GetOfflinePlayer(k.Key)?.Name ?? "(unknown)";
            Say(session, $"{i++}. {name} - {k.Value} kills");
        }
        if (top.Count == 0) Say(session, "No kills recorded yet.");
    }
}
