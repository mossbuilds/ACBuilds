using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace LuminanceLedger;

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
    // - Source/ACE.Server/WorldObjects/Player_Properties.cs:
    //     public long? AvailableLuminance { get => GetProperty(PropertyInt64.AvailableLuminance); set { ... } }
    //     public long? MaximumLuminance   { get => GetProperty(PropertyInt64.MaximumLuminance);   set { ... } }
    //   Both public, PropertyInt64-backed, same shape as the already-shipped TotalExperience/AvailableExperience
    //   pair a few lines above them in the same file. Matches the idea entry exactly.
    // - Source/ACE.Server/Managers/PropertyManager.cs:
    //     public static Property<double> GetDouble(string key, double fallback = 0.0f, bool cacheFallback = true)
    //   Confirmed public static, confirmed the actual signature/return wraps the double in a Property<double>
    //   (read via .Item), not a bare double as the idea text's usage implied - handled correctly below.
    // - Source/ACE.Server/WorldObjects/Player_Luminance.cs, EarnLuminance(long amount, XpType xpType, ShareType shareType):
    //     var questModifier = PropertyManager.GetDouble("quest_lum_modifier").Item;
    //     var modifier = PropertyManager.GetDouble("luminance_modifier").Item;
    //     if (xpType == XpType.Quest) modifier *= questModifier;
    //   Confirmed both keys are referenced exactly this way (quest_lum_modifier only multiplies in for quest-type
    //   luminance; luminance_modifier applies to all luminance). Both keys also confirmed present in
    //   PropertyManager.cs's DefaultDoubleProperties table (default 1.0 each), so GetDouble always has a real
    //   fallback even before any admin override.
    [CommandHandler("luminance", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Shows your available/maximum luminance and the active server luminance multipliers.")]
    public static void HandleLuminance(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Luminance ledger is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var available = player.AvailableLuminance ?? 0;
        var maximum = player.MaximumLuminance ?? 0;
        var percent = maximum > 0 ? (double)available / maximum * 100.0 : 0.0;

        var luminanceModifier = PropertyManager.GetDouble("luminance_modifier").Item;
        var questLumModifier = PropertyManager.GetDouble("quest_lum_modifier").Item;

        Reply(session, $"Luminance: {available:N0} / {maximum:N0} available ({percent:0.0}% to cap). " +
                        $"Server multipliers - luminance: x{luminanceModifier:0.###}, quest luminance: x{questLumModifier:0.###} " +
                        "(quest multiplier stacks on top of the base multiplier for quest-earned luminance only).");
    }
}
