using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace EnlightenmentReady;

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

    private static string Check(bool ok) => ok ? "OK" : "MISSING";

    // Verified against ACE master, re-fetched in full for this build (Source/ACE.Server/Entity/Enlightenment.cs):
    // - Enlightenment.VerifyRequirements(Player)  - public static bool - the exact path the enlightenment NPC itself
    //   calls; used here only as a source-of-truth cross-check (never called - it also sends chat lines to the
    //   player, which this read-only preview does not want to duplicate). Everything it checks is broken out below.
    // - Enlightenment.VerifyLumAugs(Player)       - public static bool - sums the 11 named LumAug* int properties
    //   and checks the total equals 65.
    // - Enlightenment.VerifySocietyMaster(Player) - public static bool - checks SocietyRankCelhan/Eldweb/Radblo == 1001.
    // - Player.Level                              - public int? (WorldObjects/Player_Properties.cs region), plain read.
    // - Player.GetFreeInventorySlots()            - public instance method on Player (ACE.Server.WorldObjects),
    //   confirmed public and callable from outside the class; same member Enlightenment.VerifyRequirements calls.
    // - Player.Enlightenment                      - public int property; accessed as `player.Enlightenment += 1`
    //   from OUTSIDE Enlightenment's declaring class in Enlightenment.AddPerks, which by this repo's external-access
    //   rule proves it is public (a private/protected member could not be written from another class).
    // - CharacterTitle.Awakened/Enlightened/Illuminated/Transcended/CosmicConscious - read directly from the same
    //   switch in Enlightenment.AddPerks; the title name for Enlightenment+1 (the title earned on the NEXT success).
    // No patches, no HandleEnlightenment call, no state changes: every member above is read-only from this mod.
    [CommandHandler("enlightencheck", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Read-only preview of all 5 Enlightenment-quest requirements at once, your current enlightenment count and your next title.")]
    public static void HandleEnlightenCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Enlightenment check is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var level = player.Level ?? 0;
        var levelOk = level >= 275;

        var lumOk = Enlightenment.VerifyLumAugs(player);
        var lumTotal = player.LumAugAllSkills + player.LumAugSurgeChanceRating + player.LumAugCritDamageRating +
                       player.LumAugCritReductionRating + player.LumAugDamageRating + player.LumAugDamageReductionRating +
                       player.LumAugItemManaUsage + player.LumAugItemManaGain + player.LumAugHealingRating +
                       player.LumAugSkilledCraft + player.LumAugSkilledSpec;

        var societyOk = Enlightenment.VerifySocietyMaster(player);

        var freeSlots = player.GetFreeInventorySlots();
        var slotsOk = freeSlots >= 25;

        var enlightenment = player.Enlightenment;
        var usesOk = enlightenment < 5;

        var allOk = levelOk && lumOk && societyOk && slotsOk && usesOk;

        var nextTitle = enlightenment switch
        {
            0 => "Awakened",
            1 => "Enlightened",
            2 => "Illuminated",
            3 => "Transcended",
            4 => "CosmicConscious",
            _ => "(max reached)"
        };

        Reply(session, $"Enlightenment check - overall: {(allOk ? "READY" : "not ready")}. " +
                        $"Level: {Check(levelOk)} ({level}/275). " +
                        $"Luminance auras: {Check(lumOk)} ({lumTotal}/65 credits). " +
                        $"Society mastery: {Check(societyOk)}. " +
                        $"Free pack slots: {Check(slotsOk)} ({freeSlots}/25). " +
                        $"Uses remaining: {Check(usesOk)} ({enlightenment}/5 used). " +
                        $"Next title on success: {nextTitle}.");
    }
}
