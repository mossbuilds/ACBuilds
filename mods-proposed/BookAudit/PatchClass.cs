using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace BookAudit;

// Patched on ACE.Server.WorldObjects.Book (Source/ACE.Server/WorldObjects/Book.cs), not Player_Book.cs's
// HandleActionBookAddPage/ModifyPage/DeletePage. Both are public and both were confirmed by a fresh full-file
// fetch of both files this round. Book's own methods are the better postfix target for THIS mod because:
//   - AddPage/ModifyPage/DeletePage each already take the acting identity as a direct parameter
//     (authorName on AddPage; the Player on ModifyPage/DeletePage), so no second lookup is needed either way.
//   - Their return value IS the real outcome: AddPage returns null on failure (page cap reached),
//     ModifyPage/DeletePage return false when Book's own authorship check no-ops the write. Player_Book.cs's
//     handlers discard that outcome entirely - HandleActionBookModifyPage always sends success:true regardless
//     of what ModifyPage returned - so patching there would have logged failed writes as if they succeeded,
//     which is exactly the "must also capture the bool success" risk flagged in the idea text.
// Never blocks or alters a write - postfix only, original method always runs first.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    // Book.AddPage(uint authorId, string authorName, string authorAccount, bool ignoreAuthor, string pageText, out int index)
    // returns PropertiesBookPageData (null on failure - page cap reached). HandleActionBookAddPage always calls this
    // with pageText "" (the client adds the page blank, then a separate ModifyPage fills it in), so "add" entries are
    // expected to show empty/short text - that mirrors the real client flow, not a bug in this mod.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Book), nameof(Book.AddPage))]
    public static void PostAddPage(Book __instance, string authorName, string pageText, PropertiesBookPageData __result)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            Log(cfg, "add", authorName, __instance, __result != null, pageText);
        }
        catch (Exception e) { ModManager.Log($"[BookAudit] add: {e.Message}"); }
    }

    // Book.ModifyPage(int index, string pageText, Player player) returns bool - false when the author check
    // (page.IgnoreAuthor / matching author identity / Sentinel / Admin) rejects the write, or the text is unchanged.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Book), nameof(Book.ModifyPage))]
    public static void PostModifyPage(Book __instance, int index, string pageText, Player player, bool __result)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            Log(cfg, "modify", player?.Name ?? "?", __instance, __result, pageText, index);
        }
        catch (Exception e) { ModManager.Log($"[BookAudit] modify: {e.Message}"); }
    }

    // Book.DeletePage(int index, Player player) returns bool - false when the author check rejects the delete
    // or the page did not exist.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Book), nameof(Book.DeletePage))]
    public static void PostDeletePage(Book __instance, int index, Player player, bool __result)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            Log(cfg, "delete", player?.Name ?? "?", __instance, __result, null, index);
        }
        catch (Exception e) { ModManager.Log($"[BookAudit] delete: {e.Message}"); }
    }

    private static void Log(Settings cfg, string action, string playerName, Book book, bool success, string? pageText, int index = -1)
    {
        var guid = book?.Guid.Full ?? 0;
        var idxPart = index >= 0 ? $" | page {index}" : "";
        string textPart;
        if (!cfg.LogFullText)
            textPart = $" | len {pageText?.Length ?? 0}";
        else
        {
            var text = (pageText ?? "").Replace("\r", " ").Replace("\n", " \\n ");
            textPart = action == "delete" ? "" : $" | text: {text}";
        }
        var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | {playerName} | {action} | book {guid:X8}{idxPart} | success {success}{textPart}";
        Write(cfg, line);
    }

    private static void Write(Settings cfg, string line)
    {
        lock (gate)
        {
            var fi = new FileInfo(cfg.LogFile);
            if (fi.Exists && fi.Length > cfg.MaxKb * 1024L)
            {
                for (var i = cfg.MaxFiles; i >= 1; i--)
                {
                    var src = i == 1 ? cfg.LogFile : $"{cfg.LogFile}.{i - 1}";
                    var dst = $"{cfg.LogFile}.{i}";
                    if (!File.Exists(src)) continue;
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(src, dst);
                }
            }
            File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
        }
    }

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    [CommandHandler("bookaudit", AccessLevel.Sentinel, CommandHandlerFlag.None, 0,
        "Shows the last N book-write audit entries (max 50) or those involving a character.", "[n | character name]")]
    public static void HandleBookAudit(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        string[] lines;
        lock (gate) lines = File.Exists(cfg.LogFile) ? File.ReadAllLines(cfg.LogFile) : Array.Empty<string>();
        var n = 10;
        IEnumerable<string> q = lines;
        if (parameters.Length > 0)
        {
            if (parameters.Length == 1 && int.TryParse(parameters[0], out var v)) n = Math.Clamp(v, 1, 50);
            else
            {
                n = 50;
                var name = string.Join(" ", parameters);
                q = lines.Where(l => l.Contains(name, StringComparison.OrdinalIgnoreCase));
            }
        }
        var list = q.TakeLast(n).ToList();
        if (list.Count == 0) { Say(session, "No book writes logged."); return; }
        foreach (var l in list) Say(session, l);
    }
}
