using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace TickLagAlert;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static int consecutiveSlow;
    private static DateTime lastAlert = DateTime.MinValue;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var every = TimeSpan.FromSeconds(Math.Max(5, Cfg.PollSeconds));
            timer = new Timer(_ =>
            {
                try { Poll(); }
                catch (Exception e) { ModManager.Log($"[TickLagAlert] {e.Message}"); }
            }, null, every, every);
        }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    // Reads the last UpdateGameWorld_Entire duration from ACE's own monitor. Returns null if the monitor
    // isn't running (Start() never called via the stock @monitor command), so we can say so instead of
    // silently reporting a false "all clear".
    private static double? LastTickSeconds()
    {
        if (!ServerPerformanceMonitor.IsRunning)
            return null;
        var history = ServerPerformanceMonitor.GetEventHistory5m(ServerPerformanceMonitor.MonitorType.UpdateGameWorld_Entire);
        return history.LastEvent;
    }

    private static void Poll()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;

        var last = LastTickSeconds();
        if (last == null)
        {
            consecutiveSlow = 0;
            return;
        }

        if (last.Value > cfg.WarnSeconds)
            consecutiveSlow++;
        else
            consecutiveSlow = 0;

        if (consecutiveSlow < Math.Max(1, cfg.SustainedTicks))
            return;

        var now = DateTime.UtcNow;
        var cooldown = TimeSpan.FromMinutes(Math.Max(1, cfg.AlertCooldownMinutes));
        if (now - lastAlert < cooldown)
            return;
        lastAlert = now;

        var msg = $"TickLagAlert: world tick running slow - last {last.Value:N3}s, over {cfg.WarnSeconds:N3}s for {consecutiveSlow} checks.";
        foreach (var p in PlayerManager.GetAllOnline())
        {
            if (p.Session == null || p.Session.AccessLevel < AccessLevel.Sentinel) continue;
            var admin = p;
            new ActionChain(admin, () =>
            {
                admin.Session?.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            }).EnqueueChain();
        }
    }

    [CommandHandler("lagwatch", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 1, "Toggles the tick-lag poll or shows current numbers.", "[on|off|status]")]
    public static void HandleLagwatch(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        var arg = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";

        void Send(string s)
        {
            if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(s, ChatMessageType.Broadcast));
            else Console.WriteLine(s);
        }

        switch (arg)
        {
            case "on":
                cfg.Enabled = true;
                if (Cfg != null) Cfg.Enabled = true;
                Send("TickLagAlert: enabled (in memory; edit Settings.json to persist).");
                break;
            case "off":
                cfg.Enabled = false;
                if (Cfg != null) Cfg.Enabled = false;
                Send("TickLagAlert: disabled.");
                break;
            default:
                var last = LastTickSeconds();
                if (last == null)
                {
                    Send("TickLagAlert: ServerPerformanceMonitor is not running - start it with the stock @monitor performance start command first, or these numbers will read as empty.");
                }
                else
                {
                    Send($"TickLagAlert: last tick {last.Value:N3}s (warn at {cfg.WarnSeconds:N3}s, {consecutiveSlow} consecutive slow checks, enabled={cfg.Enabled}).");
                }
                break;
        }
    }
}
