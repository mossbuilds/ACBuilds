using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AllegianceXpLedger;

/// <summary>
/// Player command: /allegiancexp
/// Reports the caller's own three allegiance-XP accounting properties, all confirmed public get/set on
/// Player in Source/ACE.Server/WorldObjects/Player_Allegiance.cs (fetched in full this round):
///   - AllegianceXPCached (line 23): XP a patron's vassals have produced for them but that has not yet been
///     applied to the patron's own XP total. Applied (and zeroed) by AddAllegianceXP(), called from
///     HandleAllegianceOnLogin() (line 442) when the patron next logs in.
///   - AllegianceXPGenerated (line 29): XP generated up through the current patron chain since the caller
///     last swore Allegiance - reset to 0 by HandleAllegianceOnLogin/OnSwearAllegiance's own reset at line 130
///     every time the caller swears to a (new) patron. Not a true lifetime total; worded accordingly below.
///   - AllegianceXPReceived (line 35): cumulative XP actually applied to the caller's own total from vassals,
///     incremented at line 497 (AllegianceXPReceived += AllegianceXPCached) each time AddAllegianceXP() runs.
/// Read-only: never writes any of the three properties, never calls AddAllegianceXP() or GrantXP.
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const string Tag = "[AllegianceXpLedger]";

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer.Settings;
        ModManager.Log($"{Tag} ready: /allegiancexp (player, read-only)");
        return base.OnWorldOpen();
    }

    [CommandHandler("allegiancexp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Reports your pending, received, and current-oath allegiance XP.")]
    public static void HandleAllegianceXp(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;

        session.Network.EnqueueSend(new GameMessageSystemChat(
            $"Allegiance XP - generated (since your current oath): {player.AllegianceXPGenerated:N0}, " +
            $"pending from vassals (not yet applied): {player.AllegianceXPCached:N0}, " +
            $"received (lifetime, applied from vassals): {player.AllegianceXPReceived:N0}.",
            ChatMessageType.Broadcast));

        if (player.AllegianceXPCached != 0)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat(
                "The pending amount is applied automatically the next time you log in.", ChatMessageType.Broadcast));
        }
    }
}
