using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace SettingsPeek;

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

    // Verified (ACE master PropertyManager.cs, namespace ACE.Server.Managers, public static class):
    // GetBool/GetLong/GetDouble(string key, T fallback, bool cacheFallback) return Property<T> (public struct, member .Item).
    // Reads hit a ConcurrentDictionary cache first; cacheFallback:false keeps this command from adding cache entries.
    [CommandHandler("serverrules", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows the server's main gameplay rates and rules.")]
    public static void HandleServerRules(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Server rules are not available."); return; }

        Reply(session, "Server rules:");
        foreach (var k in cfg.BoolKeys.Distinct())
            Reply(session, $"  {k}: {(PropertyManager.GetBool(k, false, false).Item ? "on" : "off")}");
        foreach (var k in cfg.LongKeys.Distinct())
            Reply(session, $"  {k}: {PropertyManager.GetLong(k, 0, false).Item}");
        foreach (var k in cfg.DoubleKeys.Distinct())
            Reply(session, $"  {k}: {PropertyManager.GetDouble(k, 0.0, false).Item:0.###}");
    }
}
