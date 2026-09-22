using ACE.Entity.Enum.Properties;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AugmentationPlanner;

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

    // Verified against ACE master, re-fetched for this build:
    // - Source/ACE.Server/WorldObjects/AugmentationDevice.cs:
    //     public static Dictionary<AugmentationType, int> MaxAugs   - per-type cap (attributes 10, resistances 2,
    //     most others 1; a handful of standalone caps like BurdenLimit=5, DeathItemLoss=3, BonusSalvage=4, RegenBonus=2,
    //     SpellDuration=5). Confirmed public static.
    //     public static Dictionary<AugmentationType, PropertyInt> AugProps  - maps each AugmentationType to the exact
    //     PropertyInt the player's current count of that augmentation is stored under. Confirmed public static.
    //     public static bool AttributeAugmentationSafetyCapEnabled  - confirmed public static, reads
    //     PropertyManager.GetBool("attribute_augmentation_safety_cap"). Not used below: it only affects the
    //     per-attribute StartingValue>=96/100 innate-value check inside VerifyRequirements, which this command
    //     does not read (see README "Not verified this round").
    // - Source/ACE.Server/WorldObjects/Player_Properties.cs:
    //     public int AugmentationInnateFamily      { get => GetProperty(PropertyInt.AugmentationInnateFamily) ?? 0; ... }
    //     public int AugmentationResistanceFamily  { get => GetProperty(PropertyInt.AugmentationResistanceFamily) ?? 0; ... }
    //     public long? AvailableExperience         { get => GetProperty(PropertyInt64.AvailableExperience); ... }
    //   All three confirmed public instance properties on the partial Player class.
    // - Source/ACE.Entity/Enum/AugmentationType.cs (AugTypeHelper, same file):
    //     public static bool IsAttribute(AugmentationType type)  - Strength..Self, shared family cap
    //     public static bool IsResist(AugmentationType type)     - ResistSlash..ResistElectric, shared family cap
    //   Both confirmed public static. Enum value 41 does not exist (gap in ACE's own numbering, "// missing 41?"
    //   comment in source) - Enum.GetValues<AugmentationType>() simply never yields it, no special-casing needed.
    // This mirrors exactly what AugmentationDevice.VerifyRequirements itself reads before allowing a real
    // augmentation (same MaxAugs/AugProps/family-counter comparisons); it never calls DoAugmentation and never
    // consumes anything.
    [CommandHandler("augcheck", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Shows which augmentations you can still take, and how many slots remain per shared category.")]
    public static void HandleAugCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Augmentation planner is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var lines = new List<string>();

        foreach (AugmentationType type in Enum.GetValues<AugmentationType>())
        {
            if (type == AugmentationType.None)
                continue;

            if (!AugmentationDevice.AugProps.TryGetValue(type, out var prop) ||
                !AugmentationDevice.MaxAugs.TryGetValue(type, out var max))
                continue;

            var current = player.GetProperty(prop) ?? 0;

            string status;
            if (AugTypeHelper.IsAttribute(type))
            {
                var family = player.AugmentationInnateFamily;
                if (family >= max)
                    status = $"family cap reached ({family}/{max} innate attribute augs)";
                else if (current >= max)
                    status = $"maxed ({current}/{max})";
                else
                    status = $"available ({current}/{max}, family {family}/{max})";
            }
            else if (AugTypeHelper.IsResist(type))
            {
                var family = player.AugmentationResistanceFamily;
                if (family >= max)
                    status = $"family cap reached ({family}/{max} resistance augs)";
                else if (current >= max)
                    status = $"maxed ({current}/{max})";
                else
                    status = $"available ({current}/{max}, family {family}/{max})";
            }
            else
            {
                status = current >= max ? $"maxed ({current}/{max})" : $"available ({current}/{max})";
            }

            lines.Add($"{type}: {status}");
        }

        var availableXp = player.AvailableExperience ?? 0;

        Reply(session, $"Available XP: {availableXp:N0}");
        foreach (var line in lines)
            Reply(session, line);
    }
}
