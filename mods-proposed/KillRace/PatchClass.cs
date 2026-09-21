using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace KillRace;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static Dictionary<uint, int> counts = new();
    private static bool running;
    private static DateTime endsAt, nextStandings;
    private static string filter = "";

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Say(Session s, string msg)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    private static string NameOf(uint guid) => PlayerManager.GetOnlinePlayer(guid)?.Name ?? PlayerManager.GetOfflinePlayer(guid)?.Name ?? "(unknown)";

    private static string Standings(int size)
    {
        List<KeyValuePair<uint, int>> top;
        lock (gate) top = counts.OrderByDescending(k => k.Value).Take(size).ToList();
        return top.Count == 0 ? "no kills yet" : string.Join(", ", top.Select((k, i) => $"{i + 1}. {NameOf(k.Key)} {k.Value}"));
    }

    private static void Tick()
    {
        try
        {
            var cfg = Cfg; if (cfg == null) return;
            bool finish = false, mid = false;
            lock (gate)
            {
                if (!running) return;
                if (DateTime.UtcNow >= endsAt) finish = true;
                else if (DateTime.UtcNow >= nextStandings) { mid = true; nextStandings = DateTime.UtcNow.AddMinutes(Math.Max(1, cfg.StandingsMinutes)); }
            }
            if (finish) Finish();
            else if (mid) Broadcast($"Kill race standings: {Standings(Math.Clamp(cfg.Size, 1, 10))}");
        }
        catch (Exception e) { ModManager.Log($"[KillRace] {e.Message}"); }
    }

    private static void Finish()
    {
        var cfg = Cfg ?? new Settings();
        List<KeyValuePair<uint, int>> all;
        lock (gate)
        {
            if (!running) return;
            running = false;
            all = counts.OrderByDescending(k => k.Value).ToList();
            counts = new();
        }
        timer?.Dispose(); timer = null;
        if (all.Count == 0) { Broadcast("The kill race is over. Nobody scored."); return; }
        var best = all[0].Value;
        var winners = all.Where(k => k.Value == best).ToList();
        Broadcast($"The kill race is over! Winner(s): {string.Join(", ", winners.Select(w => NameOf(w.Key)))} with {best} kills.");
        var reward = Math.Clamp(cfg.RewardPyreals, 0, Math.Max(0, cfg.RewardCap));
        if (reward <= 0) return;
        foreach (var w in winners)
        {
            var p = PlayerManager.GetOnlinePlayer(w.Key);
            if (p == null) continue;
            // Work on the player's own actor, never from the timer thread.
            new ActionChain(p, () =>
            {
                var coin = WorldObjectFactory.CreateNewWorldObject((uint)WeenieClassName.W_COINSTACK_CLASS);
                if (coin == null) return;
                coin.SetStackSize(reward);
                if (p.TryCreateInInventoryWithNetworking(coin))
                    p.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Kill race prize: {reward} pyreals.", ChatMessageType.Broadcast));
                else coin.Destroy();
            }).EnqueueChain();
        }
    }

    // Creature.OnDeath(DamageHistoryInfo, DamageType, bool) is public virtual; Player victims skipped.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Creature), nameof(Creature.OnDeath), new Type[] { typeof(DamageHistoryInfo), typeof(DamageType), typeof(bool) })]
    public static void PostOnDeath(Creature __instance, DamageHistoryInfo lastDamager)
    {
        try
        {
            if (Cfg == null || !Cfg.Enabled || !running || __instance is Player || lastDamager == null) return;
            if (lastDamager.TryGetAttacker() is not Player killer) return;
            lock (gate)
            {
                if (!running) return;
                if (filter.Length > 0 && (__instance.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) return;
                counts[killer.Guid.Full] = counts.GetValueOrDefault(killer.Guid.Full) + 1;
            }
        }
        catch (Exception e) { ModManager.Log($"[KillRace] {e.Message}"); }
    }

    [CommandHandler("killrace", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Run a timed monster-kill contest.", "start <minutes> [nameFilter] | stop | status")]
    public static void HandleRace(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "KillRace is switched off."); return; }
        switch (parameters[0].ToLowerInvariant())
        {
            case "start" when parameters.Length >= 2:
                if (!int.TryParse(parameters[1], out var min)) { Say(session, "Minutes must be a number."); return; }
                min = Math.Clamp(min, 1, Math.Max(1, cfg.MaxRaceMinutes));
                lock (gate)
                {
                    if (running) { Say(session, "A race is already running."); return; }
                    running = true; counts = new();
                    filter = string.Join(" ", parameters.Skip(2));
                    endsAt = DateTime.UtcNow.AddMinutes(min);
                    nextStandings = DateTime.UtcNow.AddMinutes(Math.Max(1, cfg.StandingsMinutes));
                }
                timer?.Dispose();
                timer = new Timer(_ => Tick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                Broadcast($"KILL RACE! {min} minute(s){(filter.Length > 0 ? $", only '{filter}' count" : "")}. Most monster kills wins!");
                break;
            case "stop":
                if (!running) { Say(session, "No race running."); return; }
                Finish();
                break;
            case "status":
                Say(session, running ? $"Race running, {Math.Max(0, (int)(endsAt - DateTime.UtcNow).TotalSeconds)}s left. {Standings(10)}" : "No race running.");
                break;
            default:
                Say(session, "Usage: /killrace start <minutes> [nameFilter] | stop | status");
                break;
        }
    }

    // racetop is not a built-in ACE command name.
    [CommandHandler("racetop", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Show the current kill race standings.", "")]
    public static void HandleTop(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "KillRace is switched off."); return; }
        Say(session, running ? $"Kill race: {Standings(Math.Clamp(cfg.Size, 1, 10))}" : "No kill race is running.");
    }
}
