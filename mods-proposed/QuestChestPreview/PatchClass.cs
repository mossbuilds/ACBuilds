using ACE.Common.Extensions;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace QuestChestPreview;

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

    // Free command name (checked against %TEMP%\cmds.txt - no clash with any ACE built-in): chestcheck.
    [CommandHandler("chestcheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Previews whether you currently meet a quest-gated chest's requirement, using your last-appraised chest.")]
    public static void HandleChestCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ChestCheck is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Same last-appraised-object pattern as PriceCheck/VendorStock/HouseEligibilityCheck:
        // CommandHandlerHelper.GetLastAppraisedObject() is `internal static`
        // (ACE.Server.Command.Handlers, verified) and not visible outside ACE.Server, so we
        // reproduce its body directly: read Player.RequestedAppraisalTarget (public uint?,
        // Player_Properties.cs) and resolve it with Player.FindObject(.., SearchLocations.Everywhere, ..) (public).
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise a chest first (examine it), then run /chestcheck again."); return; }

        var obj = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (obj == null) { Say(session, "Couldn't find your last-appraised object - it may no longer exist."); return; }

        if (obj is not Chest chest)
        {
            Say(session, "Your last-appraised object isn't a chest. Appraise a chest, then run /chestcheck again.");
            return;
        }

        // Deliberately NOT calling Chest.CheckUseRequirements(activator) here. That method
        // (WorldObjects/Chest.cs, `public override`, fetched in full this round) is written to
        // run inline with an actual open attempt and is not safe to call standalone for a preview:
        // beyond the quest branch it also (a) sends a lock-fail broadcast sound and a transient
        // error and returns early if the chest IsLocked, (b) mutates open/viewer state and can
        // call Close(player) if the chest is already open to this or another player, and (c) even
        // within the quest branch itself calls EmoteManager.OnQuest(player) - which fires the
        // chest's configured on-quest emote set, a real, player-visible side effect - on both the
        // "no quest yet" and "can solve" paths, and QuestManager.HandleSolveError(Quest) - which
        // sends real network chat/error messages - on the "on cooldown" path. None of that is
        // acceptable from a read-only preview command. Same caution as ComponentPrecheck's
        // TryBurnComponents check.
        //
        // Instead, this reproduces only the pure-read quest predicate from Chest.cs's own
        // `if (Quest != null)` block, calling only WorldObject.Quest (public string, re-verified
        // PropertyString.Quest-backed getter, WorldObject_Properties.cs line ~2864-2868) and
        // QuestManager.HasQuest(string) / CanSolve(string) / GetNextSolveTime(string) (all
        // `public`, Managers/QuestManager.cs, re-fetched in full this round). All three read
        // existing state only (GetQuest / DatabaseManager.World.GetCachedQuest lookups) and never
        // write to the player's quest registry, unlike Update/Stamp/Increment/SetQuestCompletions
        // on the same class - confirmed by reading every line of QuestManager.cs, not just the
        // three named methods.
        var quest = chest.Quest;
        if (quest == null)
        {
            Say(session, "This chest has no quest requirement.");
            return;
        }

        if (!player.QuestManager.HasQuest(quest))
        {
            Say(session, $"You don't have the quest flag for this chest yet (quest: {quest}).");
            return;
        }

        if (player.QuestManager.CanSolve(quest))
        {
            Say(session, $"Eligible: you meet the quest requirement for this chest (quest: {quest}).");
            return;
        }

        var nextSolveTime = player.QuestManager.GetNextSolveTime(quest);

        if (nextSolveTime == TimeSpan.MaxValue)
        {
            Say(session, $"You have solved this chest's quest ({quest}) the maximum number of times.");
        }
        else
        {
            Say(session, $"On cooldown: ready again in {nextSolveTime.GetFriendlyString()} (quest: {quest}).");
        }
    }
}
