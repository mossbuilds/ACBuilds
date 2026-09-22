using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace BurdenCheck;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Reply(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Verified against ACE master (Source/ACE.Server/WorldObjects/Player_Inventory.cs), re-fetched for this build:
    // - GetEncumbranceCapacity(): public int, body `(int)((150 * strength) + (AugmentationIncreasedCarryingCapacity * 30 * strength))`.
    //   Confirmed public, confirmed body, matches the idea entry exactly.
    // - GetAvailableBurden(): public int, body `(GetEncumbranceCapacity() * 3) - EncumbranceVal ?? 0`. Confirmed public,
    //   confirmed body, matches the idea entry exactly. EncumbranceVal is a nullable int (PropertyInt.EncumbranceVal),
    //   used elsewhere in the same file with `?? 0` guards (e.g. GameMessagePrivateUpdatePropertyInt sends
    //   `EncumbranceVal ?? 0`) - this command reads it the same defensive way rather than assuming it is always set.
    // - The hard cap used by ACE's own inventory-add checks is `GetEncumbranceCapacity() * 3` (the same multiplier
    //   baked into GetAvailableBurden() itself), confirmed by the same file's capacity-check call sites.
    // - AugmentationIncreasedCarryingCapacity: only ever seen as a bare identifier inside Player's own code (used
    //   inside GetEncumbranceCapacity()'s body), never in a declaration line or an external access chain -
    //   accessibility unverified. Not read directly anywhere in this command; GetEncumbranceCapacity() already
    //   bakes its effect into the number this command prints, so the risk does not affect anything displayed here.
    [CommandHandler("burden", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows your current carry weight vs. capacity.")]
    public static void HandleBurden(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Burden check is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var current = player.EncumbranceVal ?? 0;
        var capacity = player.GetEncumbranceCapacity();
        var hardCap = capacity * 3;
        var available = player.GetAvailableBurden();

        Reply(session, $"Burden: {current} / capacity {capacity} (hard cap {hardCap}). Available headroom: {available}.");
    }
}
