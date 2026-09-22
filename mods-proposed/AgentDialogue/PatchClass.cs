using System.Text.Json;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Managers;
using ACE.Shared.Mods;

namespace AgentDialogue;

// Hooks EmoteManager.OnHearChat and EmoteManager.OnTalkDirect (ACE.Server.WorldObjects.Managers, EmoteManager.cs) with
// Harmony postfixes so a watched NPC's inbox line fires whether or not the weenie has any HearChat/ReceiveTalkDirect
// emotes configured. Both methods are `public void On...(Player player, string message)`, called on the NPC's own
// EmoteManager instance - `__instance.WorldObject` is the NPC (or its proxy, e.g. a Hooker; see EmoteManager.WorldObject).
// No emote set is created, removed or executed by this mod - it only observes the same call ACE already makes.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new(); // guards both inbox and outbox file access
    private static HashSet<uint> watched = new();
    private static Timer? timer;
    // outbox lines waiting on an NPC that isn't currently loaded: text -> polls remaining before we give up on it
    private static readonly Dictionary<OutLine, int> pending = new();

    private record OutLine(uint Wcid, string Text);

    private static double Now() => ACE.Common.Time.GetUnixTime();

    private static string Clean(string s, int max)
    {
        var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return t.Length > max ? t[..max] : t;
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        watched = new HashSet<uint>(Cfg.WatchedWcids);
        if (Cfg.Enabled && watched.Count > 0)
        {
            var period = TimeSpan.FromSeconds(Math.Max(1, Cfg.PollSeconds));
            timer = new Timer(_ =>
            {
                try { PollOutbox(); }
                catch (Exception e) { ModManager.Log($"[AgentDialogue] poll: {e.Message}"); }
            }, null, period, period);
        }
        ModManager.Log($"[AgentDialogue] ready, enabled={Cfg.Enabled}, watched={watched.Count}");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    // --- inbox: a watched NPC heard something -----------------------------------------------------------------

    private static void AppendInbox(WorldObject npc, Player player, string message)
    {
        var cfg = Cfg;
        if (cfg == null) return;
        try
        {
            var text = Clean(message, cfg.MaxLength);
            if (text.Length == 0) return;
            var line = JsonSerializer.Serialize(new
            {
                time = Now(),
                npc_wcid = npc.WeenieClassId,
                npc_name = npc.Name,
                speaker = player.Name, // character name only - never account name, never IP
                text,
                landblock = $"{player.Location?.Landblock ?? 0:X4}"
            });
            var dir = Path.GetDirectoryName(cfg.InboxFile);
            lock (gate)
            {
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(cfg.InboxFile, line + Environment.NewLine);
            }
        }
        catch (Exception e) { ModManager.Log($"[AgentDialogue] inbox write: {e.Message}"); }
    }

    private static void OnHeard(EmoteManager __instance, Player player, string message)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || player == null) return;
            var npc = __instance.WorldObject;
            if (npc == null || !watched.Contains(npc.WeenieClassId)) return;
            AppendInbox(npc, player, message);
        }
        catch (Exception e) { ModManager.Log($"[AgentDialogue] hear: {e.Message}"); }
    }

    // EmoteManager.OnHearChat(Player player, string message) - NPC heard local chat from a nearby player (no direct target).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EmoteManager), nameof(EmoteManager.OnHearChat), new[] { typeof(Player), typeof(string) })]
    public static void PostOnHearChat(EmoteManager __instance, Player player, string message) => OnHeard(__instance, player, message);

    // EmoteManager.OnTalkDirect(Player player, string message) - a player /te @ (or right-click "talk to") the NPC directly.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EmoteManager), nameof(EmoteManager.OnTalkDirect), new[] { typeof(Player), typeof(string) })]
    public static void PostOnTalkDirect(EmoteManager __instance, Player player, string message) => OnHeard(__instance, player, message);

    // --- outbox: speak whatever the outside process queued up -------------------------------------------------

    private static WorldObject? FindLoadedNpc(uint wcid)
    {
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            if (lb == null) continue;
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                if (wo.WeenieClassId == wcid) return wo;
        }
        return null;
    }

    // Speaks verbatim as a normal Say broadcast to nearby players - the same GameMessageHearSpeech ACE itself sends
    // for a static Say emote (EmoteManager.ExecuteEmoteSet, EmoteType.Say case), so it looks like any other NPC line.
    // (A Tell would only reach one player; Say was chosen so the "conversation" is visible the way a real one is.)
    private static void Speak(WorldObject npc, string text)
    {
        var name = npc.CreatureType == CreatureType.Olthoi ? npc.Name + "&" : npc.Name;
        npc.EnqueueBroadcast(new GameMessageHearSpeech(text, name, npc.Guid.Full, ChatMessageType.Emote), WorldObject.LocalBroadcastRange);
    }

    private static void PollOutbox()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled || watched.Count == 0) return;

        List<string> lines;
        lock (gate)
            lines = File.Exists(cfg.OutboxFile) ? File.ReadAllLines(cfg.OutboxFile).ToList() : new List<string>();
        if (lines.Count == 0) return;

        var keep = new List<string>();
        var seenThisPoll = new HashSet<OutLine>();
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            uint wcid;
            string text;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                wcid = root.TryGetProperty("npc_wcid", out var w) ? w.GetUInt32() : 0;
                text = root.TryGetProperty("text", out var t) ? (t.GetString() ?? "") : "";
            }
            catch (Exception e)
            {
                ModManager.Log($"[AgentDialogue] outbox: dropping unparseable line: {e.Message}");
                continue; // malformed - never trust it, never keep it
            }

            text = Clean(text, cfg.MaxLength);
            if (wcid == 0 || text.Length == 0 || !watched.Contains(wcid))
            {
                ModManager.Log($"[AgentDialogue] outbox: refusing line for wcid {wcid} (not on watch-list or empty text).");
                continue;
            }

            var key = new OutLine(wcid, text);
            if (!seenThisPoll.Add(key)) continue; // duplicate in this batch, already queued below

            var npc = FindLoadedNpc(wcid);
            if (npc == null)
            {
                var polls = pending.TryGetValue(key, out var n) ? n + 1 : 1;
                if (polls >= Math.Max(1, cfg.MaxRetryPolls))
                {
                    pending.Remove(key);
                    ModManager.Log($"[AgentDialogue] outbox: no loaded instance of wcid {wcid} after {polls} polls, dropping line.");
                }
                else
                {
                    pending[key] = polls;
                    keep.Add(raw); // retry next poll
                }
                continue;
            }

            pending.Remove(key);
            new ActionChain(WorldManager.ActionQueue, () => Speak(npc, text)).EnqueueChain();
        }

        lock (gate)
            File.WriteAllLines(cfg.OutboxFile, keep);
    }
}
