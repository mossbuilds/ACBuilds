using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace RestartWarn;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly HashSet<int> sent = new();
    private static DateTime lastTime = DateTime.MinValue;

    // Read-only: ServerManager.ShutdownInitiated / ShutdownTime verified in ServerManager.cs (ACE.Server.Managers).
    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Tick()
    {
        var cfg = Cfg;
        if (cfg == null) return;
        if (!ServerManager.ShutdownInitiated || ServerManager.ShutdownTime == DateTime.MinValue)
        {
            sent.Clear();
            lastTime = DateTime.MinValue;
            return;
        }
        // A new or rescheduled shutdown resets what was already announced.
        if (ServerManager.ShutdownTime != lastTime)
        {
            lastTime = ServerManager.ShutdownTime;
            sent.Clear();
        }
        var left = (int)(ServerManager.ShutdownTime - DateTime.UtcNow).TotalSeconds;
        if (left <= 0) return;
        foreach (var point in cfg.WarnAtSeconds.OrderBy(p => p))
        {
            if (left <= point && !sent.Contains(point))
            {
                foreach (var p in cfg.WarnAtSeconds.Where(p => p >= point)) sent.Add(p);
                if (left >= point - 5) // skip stale points (e.g. shutdown scheduled with a shorter delay)
                    Broadcast($"Restart notice: the server shuts down in about {left} seconds. {cfg.Suffix}");
                break;
            }
        }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[RestartWarn] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    [CommandHandler("restartwhen", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows whether a server restart is scheduled.", "")]
    public static void HandleWhen(Session session, params string[] parameters)
    {
        string msg;
        if (ServerManager.ShutdownInitiated && ServerManager.ShutdownTime != DateTime.MinValue)
        {
            var left = ServerManager.ShutdownTime - DateTime.UtcNow;
            msg = left.TotalSeconds > 0
                ? $"The server restarts in about {(int)left.TotalMinutes} min {left.Seconds} sec."
                : "The server is restarting now.";
        }
        else msg = "No restart is scheduled.";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
