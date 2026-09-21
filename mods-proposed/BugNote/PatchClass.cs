using System.Text.Json;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.Entity.Actions;
using ACE.Shared.Mods;

namespace BugNote;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static List<Note> notes = new();
    private static readonly Dictionary<string, double> last = new(); // lower-case name -> last note time

    public class Note
    {
        public string Name { get; set; } = "";
        public double Time { get; set; } // unix seconds
        public string Landblock { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private static double Now() => ACE.Common.Time.GetUnixTime();

    private static void Save() // call under gate
    {
        try { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(notes)); }
        catch (Exception e) { ModManager.Log($"[BugNote] save: {e.Message}"); }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try
        {
            if (File.Exists(Cfg.DataFile))
                lock (gate) notes = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(Cfg.DataFile)) ?? new();
        }
        catch (Exception e) { ModManager.Log($"[BugNote] load: {e.Message}"); }
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    private static string Clean(string s, int max)
    {
        var t = new string(s.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return t.Length > max ? t[..max] : t;
    }

    [CommandHandler("bugnote", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
        "Leaves a short note for the admins about a bug or stuck spot.", "<text>")]
    public static void HandleBugNote(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        var p = session?.Player;
        if (p == null) return;
        var text = Clean(string.Join(" ", parameters), cfg.MaxLength);
        if (text.Length == 0) { Say(session, "Usage: /bugnote <text>"); return; }
        var key = p.Name.ToLowerInvariant();
        var now = Now();
        Note n;
        int mine;
        lock (gate)
        {
            if (last.TryGetValue(key, out var t) && now - t < cfg.CooldownSeconds)
            {
                Say(session, $"Please wait {(int)(cfg.CooldownSeconds - (now - t)) + 1}s before another note.");
                return;
            }
            last[key] = now;
            n = new Note { Name = p.Name, Time = now, Landblock = $"{p.Location?.Landblock ?? 0:X4}", Text = text };
            notes.Add(n);
            if (notes.Count > cfg.MaxNotes) notes.RemoveRange(0, notes.Count - cfg.MaxNotes);
            Save();
            mine = notes.Count(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
        }
        Say(session, $"Note saved, thank you. You have left {mine} note(s).");
        foreach (var a in PlayerManager.GetAllOnline())
            if (a.Session != null && a.Session.AccessLevel >= AccessLevel.Sentinel)
            {
                var admin = a;
                new ActionChain(admin, () => Say(admin.Session, $"[BugNote] {n.Name} @{n.Landblock}: {n.Text}")).EnqueueChain();
            }
    }

    [CommandHandler("bugnotes", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Lists the last N bug notes (default 10).", "[n]")]
    public static void HandleBugNotes(Session session, params string[] parameters)
    {
        var n = 10;
        if (parameters.Length > 0 && int.TryParse(parameters[0], out var v)) n = Math.Clamp(v, 1, 50);
        List<Note> list;
        lock (gate) list = notes.TakeLast(n).ToList();
        if (list.Count == 0) { Say(session, "No bug notes."); return; }
        foreach (var x in list)
            Say(session, $"{DateTimeOffset.FromUnixTimeSeconds((long)x.Time):yyyy-MM-dd HH:mm} {x.Name} @{x.Landblock}: {x.Text}");
    }

    [CommandHandler("bugnoteclear", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Clears all bug notes.", "")]
    public static void HandleClear(Session session, params string[] parameters)
    {
        int c;
        lock (gate) { c = notes.Count; notes.Clear(); Save(); }
        Say(session, $"Cleared {c} bug note(s).");
    }
}
