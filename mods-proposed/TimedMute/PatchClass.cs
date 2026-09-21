using System.Text.Json;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace TimedMute;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static Dictionary<string, Entry> mutes = new(); // lower-case name -> record

    public class Entry
    {
        public string Name { get; set; } = "";
        public double Until { get; set; } // unix seconds
        public string Reason { get; set; } = "";
        public string By { get; set; } = "";
    }

    private static double Now() => ACE.Common.Time.GetUnixTime(); // verified ACE.Common.Time.GetUnixTime()

    private static void Save() // call under gate
    {
        try { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(mutes.Values.ToList())); }
        catch (Exception e) { ModManager.Log($"[TimedMute] save: {e.Message}"); }
    }

    // Same property writes as stock PlayerManager.GagPlayer, with our duration. Run on the player's actor.
    private static void ApplyGag(Player p, double secondsLeft)
    {
        p.SetProperty(PropertyBool.IsGagged, true);
        p.SetProperty(PropertyFloat.GagTimestamp, Now());
        p.SetProperty(PropertyFloat.GagDuration, secondsLeft);
        p.SaveBiotaToDatabase();
    }

    private static void ClearGag(Player p)
    {
        p.RemoveProperty(PropertyBool.IsGagged);
        p.RemoveProperty(PropertyFloat.GagTimestamp);
        p.RemoveProperty(PropertyFloat.GagDuration);
        p.SaveBiotaToDatabase();
    }

    // Re-apply the remaining time (or lift) at login. Player.PlayerEnterWorld() verified in Player_Networking.cs.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnter(Player __instance)
    {
        try
        {
            Entry? e;
            lock (gate) mutes.TryGetValue(__instance.Name.ToLowerInvariant(), out e);
            if (e == null) return;
            var left = e.Until - Now();
            var p = __instance;
            new ActionChain(p, () => { if (left > 0) ApplyGag(p, left); else ClearGag(p); }).EnqueueChain();
        }
        catch (Exception ex) { ModManager.Log($"[TimedMute] {ex.Message}"); }
    }

    private static void Sweep()
    {
        var expired = new List<Entry>();
        lock (gate)
        {
            var now = Now();
            foreach (var e in mutes.Values.Where(x => x.Until <= now).ToList())
            {
                expired.Add(e);
                mutes.Remove(e.Name.ToLowerInvariant());
            }
            if (expired.Count > 0) Save();
        }
        foreach (var e in expired)
        {
            var p = PlayerManager.GetOnlinePlayer(e.Name); // verified PlayerManager.GetOnlinePlayer(string)
            if (p == null) continue;
            // timer thread: queue onto the player's own actor instead of touching it directly
            new ActionChain(p, () => ClearGag(p)).EnqueueChain();
        }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try
        {
            if (File.Exists(Cfg.DataFile))
            {
                var list = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Cfg.DataFile)) ?? new();
                lock (gate) mutes = list.ToDictionary(x => x.Name.ToLowerInvariant());
            }
        }
        catch (Exception e) { ModManager.Log($"[TimedMute] load: {e.Message}"); }
        timer = new Timer(_ =>
        {
            try { Sweep(); }
            catch (Exception e) { ModManager.Log($"[TimedMute] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    [CommandHandler("mute", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 2,
        "Mutes an online character for N minutes (auto-expires).", "<name> <minutes> [reason]  (use underscores for spaces in the name)")]
    public static void HandleMute(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        var name = parameters[0].Replace('_', ' ');
        if (!int.TryParse(parameters[1], out var minutes)) minutes = cfg.DefaultMinutes;
        minutes = Math.Clamp(minutes, 1, cfg.MaxMinutes);
        var reason = string.Join(" ", parameters.Skip(2));
        var p = PlayerManager.GetOnlinePlayer(name);
        if (p == null) { Say(session, $"{name} is not online."); return; }
        if (p.Session != null && p.Session.AccessLevel >= AccessLevel.Advocate) { Say(session, "Refusing to mute staff."); return; }
        var secs = minutes * 60.0;
        lock (gate)
        {
            mutes[p.Name.ToLowerInvariant()] = new Entry { Name = p.Name, Until = Now() + secs, Reason = reason, By = session?.Player?.Name ?? "console" };
            Save();
        }
        new ActionChain(p, () => ApplyGag(p, secs)).EnqueueChain();
        Say(session, $"{p.Name} muted for {minutes} minute(s).");
    }

    [CommandHandler("unmute", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
        "Lifts a TimedMute early.", "<name>")]
    public static void HandleUnmute(Session session, params string[] parameters)
    {
        var name = parameters[0].Replace('_', ' ');
        bool had;
        lock (gate) { had = mutes.Remove(name.ToLowerInvariant()); if (had) Save(); }
        var p = PlayerManager.GetOnlinePlayer(name);
        if (p != null) new ActionChain(p, () => ClearGag(p)).EnqueueChain();
        Say(session, had || p != null ? $"{name} unmuted." : $"No mute found for {name}.");
    }

    [CommandHandler("mutes", AccessLevel.Sentinel, CommandHandlerFlag.None, 0, "Lists active timed mutes.", "")]
    public static void HandleMutes(Session session, params string[] parameters)
    {
        List<Entry> list;
        lock (gate) list = mutes.Values.ToList();
        if (list.Count == 0) { Say(session, "No active timed mutes."); return; }
        foreach (var e in list)
            Say(session, $"{e.Name}: {Math.Max(0, (int)((e.Until - Now()) / 60))} min left, by {e.By}{(e.Reason.Length > 0 ? " - " + e.Reason : "")}");
    }
}
