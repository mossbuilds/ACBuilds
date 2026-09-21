using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PathChoice;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private const string ChangedQuest = "pathchoice_changed"; // its LastTimeCompleted (unix s) drives the cooldown

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    /// <summary>Public API: the lower-case path name of this player, or null (none chosen / mod disabled).</summary>
    public static string? GetPath(Player? p)
    {
        var cfg = Cfg;
        if (p == null || cfg == null || !cfg.Enabled) return null;
        foreach (var d in cfg.Paths)
            if (p.QuestManager.HasQuest(cfg.QuestPrefix + d.Name)) return d.Name;
        return null;
    }

    private static void Clear(Player p, Settings cfg)
    {
        foreach (var d in cfg.Paths) p.QuestManager.Erase(cfg.QuestPrefix + d.Name);
    }

    [CommandHandler("path", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Show or choose your path.", "[choose <name>] | Admin: reset <player name>")]
    public static void HandlePath(Session session, params string[] parameters)
    {
        var p = session?.Player;
        var cfg = Cfg;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session!, "Paths are not enabled."); return; }

        if (parameters.Length >= 1 && parameters[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            if (session!.AccessLevel < AccessLevel.Admin) { Say(session, "Only an admin can reset a path."); return; }
            var name = string.Join(" ", parameters.Skip(1));
            var t = PlayerManager.GetOnlinePlayer(name);
            if (t == null) { Say(session, "That player is not online."); return; }
            new ActionChain(t, () => { Clear(t, cfg); t.QuestManager.Erase(ChangedQuest); }).EnqueueChain();
            Say(session, $"Path of {t.Name} reset.");
            return;
        }

        if (parameters.Length >= 2 && parameters[0].Equals("choose", StringComparison.OrdinalIgnoreCase))
        {
            var d = cfg.Paths.FirstOrDefault(x => x.Name.Equals(parameters[1], StringComparison.OrdinalIgnoreCase));
            if (d == null) { Say(session!, "No such path. Type /path for the list."); return; }
            var cur = GetPath(p);
            if (cur != null)
            {
                if (cur == d.Name) { Say(session!, "You already walk that path."); return; }
                if (!cfg.AllowChange) { Say(session!, "Your path is chosen. Ask an admin to reset it."); return; }
                var q = p.QuestManager.GetQuest(ChangedQuest);
                if (q != null)
                {
                    long left = q.LastTimeCompleted + cfg.ChangeCooldownHours * 3600L - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    if (left > 0) { Say(session!, $"You can change again in {left / 3600 + 1} hour(s)."); return; }
                }
            }
            new ActionChain(p, () =>
            {
                Clear(p, cfg);
                p.QuestManager.Stamp(cfg.QuestPrefix + d.Name);
                if (cur != null) { p.QuestManager.Erase(ChangedQuest); p.QuestManager.Stamp(ChangedQuest); }
                if (d.TitleId != 0) p.AddTitle(d.TitleId, false); // ignores ids not in CharacterTitle
            }).EnqueueChain();
            Say(session!, $"You walk the path of the {d.Title}.");
            return;
        }

        var mine = GetPath(p);
        var mineDef = cfg.Paths.FirstOrDefault(x => x.Name == mine);
        Say(session!, mineDef != null ? $"Your path: {mineDef.Title}. {mineDef.Description}" : "You have not chosen a path.");
        foreach (var d in cfg.Paths) Say(session!, $"  {d.Name}: {d.Title} - {d.Description}");
        if (mine == null) Say(session!, "Choose with /path choose <name>.");
    }
}
