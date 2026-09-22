using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace SquelchAudit;

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

    [CommandHandler("squelchcheck", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1,
        "Reports whether a player has any active chat squelches, without viewing or changing the squelch list itself.",
        "<player name>")]
    public static void HandleSquelchCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "SquelchAudit is not available."); return; }

        if (parameters.Length == 0 || string.IsNullOrWhiteSpace(parameters[0]))
        {
            Say(session, "Usage: /squelchcheck <player name>");
            return;
        }

        var targetName = string.Join(" ", parameters);

        // PlayerManager.FindByName(string, out bool isOnline) is public static (Managers/PlayerManager.cs,
        // verified) and returns IPlayer - the shared online/offline player interface. Only the online Player
        // instance carries a live SquelchManager (built in Player.SetEphemeralValues, an in-memory object,
        // not a database-backed one), so this command only reports for a player who is currently online.
        var found = PlayerManager.FindByName(targetName, out var isOnline);

        if (found == null)
        {
            Say(session, $"No player found named '{targetName}'.");
            return;
        }

        if (!isOnline || found is not Player player)
        {
            Say(session, $"{found.Name} is not online - squelch state is only readable while a player is in the world.");
            return;
        }

        // Player.SquelchManager is a public field (WorldObjects/Player.cs, verified) and Player.SquelchGlobal
        // is a public property (WorldObjects/Player_Properties.cs, verified) - both confirmed externally
        // accessible because Managers/PlayerManager.cs's own BroadcastToChannel reads
        // player.SquelchManager.Squelches.Contains(sender) directly from outside the Player class.
        // SquelchManager.HasSquelches and SquelchManager.Squelches (a public SquelchDB field) are both public
        // (WorldObjects/Managers/SquelchManager.cs, verified). This command only reads these - it never calls
        // any of SquelchManager's HandleActionModify*Squelch methods, so no squelch is ever added or removed.
        var mgr = player.SquelchManager;

        Say(session, $"--- Squelch audit: {player.Name} ---");

        if (!mgr.HasSquelches)
        {
            Say(session, "No active squelches - this player has not squelched any character, account, or channel.");
        }
        else
        {
            Say(session, $"Squelched characters: {mgr.Squelches.Characters.Count}");
            Say(session, $"Squelched accounts: {mgr.Squelches.Accounts.Count}");
            Say(session, $"Squelched global filters: {mgr.Squelches.Globals.Filters.Count}");
        }

        var globalMask = player.SquelchGlobal;

        Say(session, globalMask == SquelchMask.None
            ? "Global channel squelch (@filter): none."
            : $"Global channel squelch (@filter): {globalMask}");

        Say(session, "This is a read-only report - nothing was squelched, unsquelched, or otherwise changed.");
    }
}
