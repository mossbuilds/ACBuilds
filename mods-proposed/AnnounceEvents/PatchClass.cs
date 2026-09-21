using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace AnnounceEvents;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Timer? timer;
    private static int idx;
    private static string? activeEvent;

    // PlayerManager.BroadcastToAll(GameMessage) verified in PlayerManager.cs.
    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    public override Task OnWorldOpen()
    {
        var cfg = SettingsContainer.Settings;
        if (cfg.Messages.Count > 0 && cfg.IntervalSeconds > 0)
        {
            var period = TimeSpan.FromSeconds(cfg.IntervalSeconds);
            timer = new Timer(_ =>
            {
                try { Broadcast(cfg.Messages[idx++ % cfg.Messages.Count]); }
                catch (Exception e) { ModManager.Log($"[AnnounceEvents] {e.Message}"); }
            }, null, period, period);
        }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    [CommandHandler("announce", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Announce an event start/stop to all players.", "start <name> | stop | say <text>")]
    public static void HandleEvent(Session session, params string[] parameters)
    {
        var rest = string.Join(" ", parameters.Skip(1));
        switch (parameters[0].ToLowerInvariant())
        {
            case "start" when rest.Length > 0:
                activeEvent = rest;
                Broadcast($"EVENT STARTED: {rest}!");
                break;
            case "stop":
                if (activeEvent == null) { Say(session, "No event running."); return; }
                Broadcast($"Event ended: {activeEvent}. Thanks for playing!");
                activeEvent = null;
                break;
            case "say" when rest.Length > 0:
                Broadcast(rest);
                break;
            default:
                Say(session, "Usage: /announce start <name> | stop | say <text>");
                break;
        }
    }
}
