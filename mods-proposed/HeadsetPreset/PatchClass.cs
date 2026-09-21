using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace HeadsetPreset;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings cfg = new();

    public override Task OnWorldOpen()
    {
        cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Out(Session s, string msg)
    {
        if (s?.Player != null) s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else ModManager.Log("[HeadsetPreset] " + msg);
    }

    // headset/lite/lowmem/light checked against %TEMP%\cmds.txt: none are built-ins.
    [CommandHandler("headset", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show server properties that affect client load (read only).", "")]
    public static void Handle(Session session, params string[] parameters)
    {
        if (!cfg.Enabled) { Out(session, "HeadsetPreset is not enabled."); return; }
        Out(session, $"teleport_visibility_fix = {PropertyManager.GetLong("teleport_visibility_fix").Item} (0 = off; higher values RE-SEND more objects after teleports; leave 0 if a headset crashes).");
        Out(session, $"mob_awareness_range = {PropertyManager.GetDouble("mob_awareness_range").Item} (monster aggro distance only; no effect on client memory).");
        Out(session, "ACE has no per-player or global visibility-radius/object-cap property. To change a property use the built-in /modifylong or /modifydouble <name> <value> (Admin); this command changes nothing.");
    }
}
