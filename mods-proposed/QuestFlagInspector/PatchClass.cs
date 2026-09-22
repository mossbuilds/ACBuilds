using System.Linq;
using ACE.Common.Extensions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace QuestFlagInspector;

// IDEAS.md idea 74, Round 12: a Sentinel command that dumps a target player's full quest-flag registry
// to the caller's own chat. ACE's own QuestManager.ShowQuests(Player) already walks this exact registry
// but every line goes to Console.WriteLine - the server console window - never to any player, which
// makes it useless to a remote admin or a content author standing next to the test character in-world.
//
// Every member below was re-verified against a fresh full-file fetch of ACE.Server/Managers/QuestManager.cs
// (ACEmulator/ACE, master branch) and ACE.Server/WorldObjects/Creature.cs, not inferred from bare-identifier
// usage:
//   Creature.QuestManager - public get-only property (Creature.cs: "public QuestManager QuestManager { get { ... } }"),
//       lazily constructs a QuestManager(this) on first access. Player derives from Creature, so
//       player.QuestManager is externally accessible the same way PathChoice (this repo) already calls
//       p.QuestManager.HasQuest/.Erase/.Stamp from outside the Player class.
//   QuestManager.GetQuests() - public, "This is mostly used for information/debugging", returns
//       ICollection<CharacterPropertiesQuestRegistry> (a clone - "You should not mutate the results").
//       For a Player this reads player.Character.GetQuests(player.CharacterDatabaseLock).
//   QuestManager.GetCurrentSolves(string) / GetMaxSolves(string) / GetNextSolveTime(string) - all public,
//       same file, all three already used identically by this repo's own PathChoice/ContractTracker mods.
//   QuestManager.ShowQuests(Player) - public, confirmed to route every line through Console.WriteLine only:
//       "Console.WriteLine("Quest Name: " + quest.QuestName);" etc. - never a GameMessageSystemChat, never
//       reaching the calling Sentinel or any player. This mod mirrors its exact field selection (quest name,
//       times completed, last time completed) but sends to the requesting Sentinel's own chat instead, and
//       adds the per-entry cooldown/max-solves state that stock ShowQuests does not compute at all.
//
// CharacterPropertiesQuestRegistry.QuestName / NumTimesCompleted / LastTimeCompleted are the same public
// fields ShowQuests itself reads directly off each entry.
//
// Read-only: this mod never calls Update/Erase/SetQuestCompletions/Stamp. It only reads.
//
// "questflags" re-confirmed free against a fresh dump of %TEMP%\cmds.txt (327 built-ins, zero hits;
// the only other quest-shaped built-in is "myquests", a different, player-facing command).
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // Describes a single registry entry's cooldown state the same way GetNextSolveTime's own contract
    // documents it: TimeSpan.MinValue = can solve now, TimeSpan.MaxValue = max solves reached (or the
    // world quest definition is gone), anything else = time remaining.
    private static string DescribeCooldown(Player target, string questName)
    {
        var maxSolves = target.QuestManager.GetMaxSolves(questName);
        var current = target.QuestManager.GetCurrentSolves(questName);
        var next = target.QuestManager.GetNextSolveTime(questName);

        var solvesPart = maxSolves > -1 ? $"{current}/{maxSolves} solves" : $"{current} solves (unlimited)";

        string statePart;
        if (next == TimeSpan.MaxValue)
            statePart = maxSolves > -1 && current >= maxSolves ? "max solves reached" : "quest definition not found";
        else if (next == TimeSpan.MinValue)
            statePart = "ready now";
        else
            statePart = $"ready in {next.GetFriendlyString()}";

        return $"{solvesPart}, {statePart}";
    }

    [CommandHandler("questflags", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
        "Dumps a target player's full quest-flag registry to your own chat, read-only.",
        "<player name>")]
    public static void HandleQuestFlags(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "QuestFlagInspector is not available."); return; }

        if (parameters.Length == 0 || string.IsNullOrWhiteSpace(parameters[0]))
        {
            Say(session, "Usage: /questflags <player name>");
            return;
        }

        var targetName = string.Join(" ", parameters);

        // PlayerManager.FindByName(string, out bool isOnline) is public static (Managers/PlayerManager.cs,
        // already verified by this repo's own SquelchAudit mod) and returns IPlayer. QuestManager.GetQuests()
        // reads player.Character.GetQuests(player.CharacterDatabaseLock), which needs a live loaded Player -
        // so, like SquelchAudit, this only reports for a target currently online.
        var found = PlayerManager.FindByName(targetName, out var isOnline);

        if (found == null)
        {
            Say(session, $"No player found named '{targetName}'.");
            return;
        }

        if (!isOnline || found is not Player target)
        {
            Say(session, $"{found.Name} is not online - quest flags are only readable while a player is in the world.");
            return;
        }

        var quests = target.QuestManager.GetQuests();

        Say(session, $"--- Quest flags: {target.Name} ({quests.Count} total) ---");

        if (quests.Count == 0)
        {
            Say(session, "No quests in progress for this player.");
            return;
        }

        var maxLines = cfg.MaxLines > 0 ? cfg.MaxLines : int.MaxValue;
        var shown = 0;

        foreach (var quest in quests.OrderBy(q => q.QuestName, StringComparer.OrdinalIgnoreCase))
        {
            if (shown >= maxLines)
            {
                Say(session, $"... {quests.Count - shown} more not shown (MaxLines={cfg.MaxLines}).");
                break;
            }

            var lastCompleted = quest.LastTimeCompleted > 0
                ? DateTimeOffset.FromUnixTimeSeconds(quest.LastTimeCompleted).UtcDateTime.ToString("u")
                : "never";

            Say(session, $"{quest.QuestName}: {DescribeCooldown(target, quest.QuestName)}, last completed {lastCompleted}");
            shown++;
        }

        Say(session, "This is a read-only report - nothing was stamped, erased, or otherwise changed.");
    }
}
