using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace LoginGreeter;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Send(Player p, string text) =>
        p.Session?.Network.EnqueueSend(new GameMessageSystemChat(string.Format(text, p.Name), ChatMessageType.Broadcast));

    // Player.PlayerEnterWorld verified in Player_Networking.cs; it increments Character.TotalLogins before we run.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnter(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            var p = __instance;
            bool first = p.Character.TotalLogins <= 1;
            new ActionChain(p, () => { }).AddDelaySeconds(cfg.DelaySeconds).AddAction(p, () =>
            {
                try
                {
                    Send(p, first ? cfg.FirstLoginMessage : cfg.ReturningMessage);
                    if (!string.IsNullOrEmpty(cfg.OnlineMessage))
                        p.Session?.Network.EnqueueSend(new GameMessageSystemChat(string.Format(cfg.OnlineMessage, PlayerManager.GetOnlineCount()), ChatMessageType.Broadcast));
                    if (first) foreach (var t in cfg.FirstLoginTips) Send(p, t);
                }
                catch (Exception e) { ModManager.Log($"[LoginGreeter] {e.Message}"); }
            }).EnqueueChain();
        }
        catch (Exception e) { ModManager.Log($"[LoginGreeter] {e.Message}"); }
    }

    [CommandHandler("rules", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows the server rules.", "")]
    public static void HandleRules(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null) return;
        foreach (var line in cfg.Rules)
            session.Network.EnqueueSend(new GameMessageSystemChat(string.Format(line, session.Player.Name), ChatMessageType.Broadcast));
    }
}
