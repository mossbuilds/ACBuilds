using System.Text.Json;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace MilestoneRewards;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    // character guid -> milestone levels already rewarded (survives relog and restart, keyed by guid)
    private static Dictionary<uint, HashSet<int>> granted = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try { granted = JsonSerializer.Deserialize<Dictionary<uint, HashSet<int>>>(File.ReadAllText(Cfg.DataFile)) ?? new(); } catch { }
        return base.OnWorldOpen();
    }

    private static void Save()
    {
        try { lock (gate) { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(granted)); } }
        catch (Exception e) { ModManager.Log($"[MilestoneRewards] save failed: {e.Message}"); }
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // Player.CheckForLevelup() is private void in Player_Xp.cs (namespace ACE.Server.WorldObjects), so it is patched by name.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "CheckForLevelup")]
    public static void PostCheckForLevelup(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || __instance.Session == null) return;
            var level = __instance.Level ?? 0;
            var total = 0;
            var reached = new List<int>();
            lock (gate)
            {
                if (!granted.TryGetValue(__instance.Guid.Full, out var done)) granted[__instance.Guid.Full] = done = new();
                foreach (var kv in cfg.LevelToCredits.OrderBy(k => k.Key))
                {
                    if (kv.Key > level || done.Contains(kv.Key)) continue;
                    var c = Math.Clamp(kv.Value, 0, Math.Max(0, cfg.MaxCreditsPerMilestone));
                    done.Add(kv.Key); // mark even if 0 so it is never revisited
                    if (c > 0) { total += c; reached.Add(kv.Key); }
                }
            }
            if (reached.Count == 0) { return; }
            Save();
            __instance.AddSkillCredits(total);
            Say(__instance.Session, $"Milestone reached (level {string.Join(", ", reached)}): +{total} skill credit(s).");
        }
        catch (Exception e) { ModManager.Log($"[MilestoneRewards] {e.Message}"); }
    }

    // "milestones" is not a built-in ACE command name (grepped [CommandHandler] names).
    [CommandHandler("milestones", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Show your next level milestone reward.", "")]
    public static void HandleMilestones(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "Milestone rewards are switched off."); return; }
        var p = session.Player;
        var level = p.Level ?? 0;
        HashSet<int> done;
        lock (gate) done = granted.TryGetValue(p.Guid.Full, out var d) ? new(d) : new();
        var next = cfg.LevelToCredits.Where(k => k.Key > level || !done.Contains(k.Key)).OrderBy(k => k.Key).FirstOrDefault();
        if (next.Key == 0) { Say(session, "No more milestone rewards for you."); return; }
        var c = Math.Clamp(next.Value, 0, Math.Max(0, cfg.MaxCreditsPerMilestone));
        Say(session, $"Next milestone: level {next.Key} for {c} skill credit(s). You are level {level}.");
    }
}
