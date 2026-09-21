using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace XpBoost;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log($"[XpBoost] ready, multiplier {Cfg.Multiplier}");
        return base.OnWorldOpen();
    }

    // Player.EarnXP(long amount, XpType xpType, ShareType shareType = ShareType.All) - verified in Player_Xp.cs; it calls GrantXP.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.EarnXP), new Type[] { typeof(long), typeof(XpType), typeof(ShareType) })]
    public static void PreEarnXP(Player __instance, ref long amount, XpType xpType)
    {
        var c = Cfg;
        if (c == null || c.Multiplier == 1.0 || amount <= 0) return;
        if (!c.XpTypes.Contains(xpType.ToString())) return;
        if (c.MaxLevel > 0 && (__instance.Level ?? 1) > c.MaxLevel) return;
        amount = (long)(amount * c.Multiplier);
    }

    [CommandHandler("xpboost", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show or set the XP multiplier (runtime only, not saved).", "[multiplier 0.1-100]")]
    public static void HandleXpBoost(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        if (parameters.Length > 0 && double.TryParse(parameters[0], out var m) && m >= 0.1 && m <= 100) c.Multiplier = m;
        var msg = $"XpBoost multiplier: {c.Multiplier}x (types: {string.Join(",", c.XpTypes)}, max level {c.MaxLevel})";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
