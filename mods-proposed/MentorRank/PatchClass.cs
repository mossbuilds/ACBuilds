using System.Text.Json;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace MentorRank;

public class Pair
{
    public uint Mentor { get; set; }
    public uint Apprentice { get; set; }
    public HashSet<int> Rewarded { get; set; } = new();
}

public class Data
{
    public List<Pair> Pairs { get; set; } = new();
    /// <summary>character guid -> unix seconds when it last left a pairing.</summary>
    public Dictionary<uint, long> LeftAt { get; set; } = new();
}

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static Data data = new();
    private static readonly Dictionary<uint, (uint mentor, long expiry)> invites = new();
    private static readonly Dictionary<uint, List<long>> recent = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try { data = JsonSerializer.Deserialize<Data>(File.ReadAllText(Cfg.DataFile)) ?? new(); } catch { }
        return base.OnWorldOpen();
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static void Save()
    {
        try { lock (gate) { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(data)); } }
        catch (Exception e) { ModManager.Log($"[MentorRank] save failed: {e.Message}"); }
    }

    private static void Tell(Player? p, string msg) =>
        p?.Session?.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    private static string NameOf(uint g) => PlayerManager.GetOnlinePlayer(g)?.Name ?? PlayerManager.GetOfflinePlayer(g)?.Name ?? "(unknown)";

    // caller holds gate
    private static Pair? PairOf(uint guid) => data.Pairs.FirstOrDefault(x => x.Mentor == guid || x.Apprentice == guid);

    private static bool OnCooldown(uint guid, Settings cfg) =>
        data.LeftAt.TryGetValue(guid, out var t) && Now() - t < cfg.RepairCooldownHours * 3600L;

    // "mentor" is not a built-in ACE command (checked against the 327-name list; apprentice/mentorship also free).
    [CommandHandler("mentor", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Mentoring: invite <name>, accept, leave, status (admin: list).", "")]
    public static void HandleMentor(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var me = session.Player;
        if (cfg == null || !cfg.Enabled) { Tell(me, "Mentoring is switched off."); return; }
        var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";
        try
        {
            switch (sub)
            {
                case "invite": Invite(me, cfg, string.Join(" ", parameters.Skip(1))); break;
                case "accept": Accept(me, cfg); break;
                case "leave": Leave(me); break;
                case "list":
                    if (session.AccessLevel < AccessLevel.Admin) { Tell(me, "Admin only."); break; }
                    lock (gate)
                    {
                        Tell(me, $"{data.Pairs.Count} pairing(s).");
                        foreach (var x in data.Pairs) Tell(me, $"{NameOf(x.Mentor)} -> {NameOf(x.Apprentice)} (rewarded: {string.Join(",", x.Rewarded)})");
                    }
                    break;
                default:
                    lock (gate)
                    {
                        var x = PairOf(me.Guid.Full);
                        if (x == null) { Tell(me, $"You are not in a pairing. Mentors need level {cfg.MinMentorLevel}+, apprentices level {cfg.MaxApprenticeLevel} or lower."); break; }
                        Tell(me, x.Mentor == me.Guid.Full ? $"You mentor {NameOf(x.Apprentice)}." : $"Your mentor is {NameOf(x.Mentor)}.");
                    }
                    break;
            }
        }
        catch (Exception e) { ModManager.Log($"[MentorRank] {e.Message}"); }
    }

    private static void Invite(Player me, Settings cfg, string name)
    {
        var target = string.IsNullOrWhiteSpace(name) ? null : PlayerManager.GetOnlinePlayer(name.Trim());
        if (target == null || target == me) { Tell(me, "That player is not online."); return; }
        if ((me.Level ?? 0) < cfg.MinMentorLevel) { Tell(me, $"You must be level {cfg.MinMentorLevel} to mentor."); return; }
        if ((target.Level ?? 0) > cfg.MaxApprenticeLevel) { Tell(me, $"{target.Name} is too high a level to be an apprentice."); return; }
        if ((me.Level ?? 0) - (target.Level ?? 0) < cfg.MinLevelGap) { Tell(me, $"You must be {cfg.MinLevelGap} levels above the apprentice."); return; }
        lock (gate)
        {
            if (PairOf(me.Guid.Full) != null || PairOf(target.Guid.Full) != null) { Tell(me, "One of you is already in a pairing."); return; }
            if (OnCooldown(me.Guid.Full, cfg) || OnCooldown(target.Guid.Full, cfg)) { Tell(me, "One of you is still on the re-pairing cooldown."); return; }
            if (data.Pairs.Count(x => x.Mentor == me.Guid.Full) >= cfg.MaxApprenticesPerMentor) { Tell(me, "You have too many apprentices."); return; }
            invites[target.Guid.Full] = (me.Guid.Full, Now() + cfg.InviteTimeoutSeconds);
        }
        Tell(me, $"Invitation sent to {target.Name}.");
        Tell(target, $"{me.Name} offers to mentor you. Type /mentor accept within {cfg.InviteTimeoutSeconds}s.");
    }

    private static void Accept(Player me, Settings cfg)
    {
        Player? mentor;
        lock (gate)
        {
            if (!invites.Remove(me.Guid.Full, out var inv) || inv.expiry < Now()) { Tell(me, "No pending invitation."); return; }
            mentor = PlayerManager.GetOnlinePlayer(inv.mentor);
            if (mentor == null || (me.Level ?? 0) > cfg.MaxApprenticeLevel || (mentor.Level ?? 0) < cfg.MinMentorLevel
                || PairOf(me.Guid.Full) != null || PairOf(mentor.Guid.Full) != null
                || data.Pairs.Count(x => x.Mentor == mentor.Guid.Full) >= cfg.MaxApprenticesPerMentor)
            { Tell(me, "That invitation is no longer valid."); return; }
            data.Pairs.Add(new Pair { Mentor = mentor.Guid.Full, Apprentice = me.Guid.Full });
        }
        Save();
        Tell(me, $"{mentor.Name} is now your mentor.");
        Tell(mentor, $"{me.Name} is now your apprentice.");
    }

    private static void Leave(Player me)
    {
        Pair? x;
        lock (gate)
        {
            x = PairOf(me.Guid.Full);
            if (x == null) { Tell(me, "You are not in a pairing."); return; }
            data.Pairs.Remove(x);
            data.LeftAt[x.Mentor] = Now();
            data.LeftAt[x.Apprentice] = Now();
        }
        Save();
        var other = PlayerManager.GetOnlinePlayer(x.Mentor == me.Guid.Full ? x.Apprentice : x.Mentor);
        Tell(me, "You left the mentoring pairing.");
        Tell(other, $"{me.Name} ended the mentoring pairing.");
    }

    // Player.CheckForLevelup() is private void in Player_Xp.cs (ACE.Server.WorldObjects), patched by name as in MilestoneRewards.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "CheckForLevelup")]
    public static void PostCheckForLevelup(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || __instance.Session == null) return;
            var level = __instance.Level ?? 0;
            Player? mentor = null;
            int pyr = 0, cred = 0;
            var hit = new List<int>();
            lock (gate)
            {
                var pair = data.Pairs.FirstOrDefault(x => x.Apprentice == __instance.Guid.Full);
                if (pair == null) return;
                mentor = PlayerManager.GetOnlinePlayer(pair.Mentor);
                foreach (var m in cfg.MilestoneLevels.OrderBy(v => v))
                {
                    if (m > level || pair.Rewarded.Contains(m)) continue;
                    // Not together: the milestone stays open and is retried at the next level-up.
                    if (mentor == null || !Near(mentor, __instance, cfg)) continue;
                    if (!recent.TryGetValue(pair.Mentor, out var list)) recent[pair.Mentor] = list = new();
                    list.RemoveAll(t => Now() - t > 3600);
                    if (list.Count >= cfg.MaxRewardsPerMentorPerHour) continue;
                    list.Add(Now());
                    pair.Rewarded.Add(m);
                    hit.Add(m);
                    pyr += Math.Clamp(cfg.RewardPyreals, 0, Math.Max(0, cfg.MaxPyrealsPerReward));
                    cred += Math.Clamp(cfg.RewardSkillCredits, 0, Math.Max(0, cfg.MaxCreditsPerReward));
                }
            }
            if (hit.Count == 0 || mentor == null) return;
            Save();
            var m2 = mentor;
            var apprenticeName = __instance.Name;
            var levels = string.Join(", ", hit);
            Tell(__instance, $"{m2.Name}'s guidance helped you reach level {levels}.");
            // Work on the mentor's own actor; this postfix runs on the apprentice's.
            new ActionChain(m2, () =>
            {
                if (cred > 0) m2.AddSkillCredits(cred);
                if (pyr > 0)
                {
                    var coin = WorldObjectFactory.CreateNewWorldObject((uint)WeenieClassName.W_COINSTACK_CLASS);
                    if (coin != null)
                    {
                        coin.SetStackSize(pyr);
                        if (!m2.TryCreateInInventoryWithNetworking(coin)) coin.Destroy();
                    }
                }
                Tell(m2, $"Mentor reward: {apprenticeName} reached level {levels}" +
                    (pyr > 0 ? $" (+{pyr} pyreals)" : "") + (cred > 0 ? $" (+{cred} skill credit)" : "") + ".");
            }).EnqueueChain();
        }
        catch (Exception e) { ModManager.Log($"[MentorRank] {e.Message}"); }
    }

    private static bool Near(Player a, Player b, Settings cfg)
    {
        if (cfg.RequireFellowship && (a.Fellowship == null || a.Fellowship != b.Fellowship)) return false;
        return a.Location.Landblock == b.Location.Landblock && a.Location.DistanceTo(b.Location) <= cfg.MaxDistance;
    }
}
