using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace EventClock;

/// Approach: calls ACE's own event system directly (EventManager.StartEvent/StopEvent), the same calls behind the
/// Sentinel @event_start/@event_stop commands - it creates no new events, only toggles ones an operator already
/// defined in ace_world. No world object, no database access, no account data. If the server was down when a
/// boundary passed, the missed transition is not caught up - the next tick just applies whatever the clock says
/// right now (a window already open is started late; a window already closed stays off).
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    // Our own idea of which scheduled events are currently "on" - EventManager has no bulk query, so this is the
    // only ground truth for what we started; GetEventStatus (per name) is used for the /eventclock status readout.
    private static readonly Dictionary<string, bool> active = new(StringComparer.OrdinalIgnoreCase);

    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Same day/time-window logic as PkNight's InWindow: HH:mm compare against DateTime.Now, Start<=End is a same-day
    // window, Start>End crosses midnight and belongs to the day it starts on. Empty Days = every day.
    private static bool InWindow(ScheduledEvent s, DateTime t)
    {
        if (!TimeSpan.TryParse(s.Start, out var st) || !TimeSpan.TryParse(s.End, out var en)) return false;
        bool DayOk(DateTime d) => s.Days.Count == 0 || s.Days.Any(x => string.Equals(x, d.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
        var now = t.TimeOfDay;
        if (st <= en)
            return DayOk(t) && now >= st && now < en;
        // crosses midnight: the window belongs to the day it starts on
        if (DayOk(t) && now >= st) return true;
        if (DayOk(t.AddDays(-1)) && now < en) return true;
        return false;
    }

    private static void Tick()
    {
        var c = Cfg;
        if (c == null || !c.Enabled) return;
        var now = DateTime.Now;
        foreach (var s in c.Events)
        {
            if (string.IsNullOrWhiteSpace(s.EventName)) continue;
            if (!EventManager.IsEventAvailable(s.EventName))
            {
                ModManager.Log($"[EventClock] '{s.EventName}' is not a known ACE event (check ace_world.events) - skipping.");
                continue;
            }
            var wantOn = InWindow(s, now);
            active.TryGetValue(s.EventName, out var isOn);
            if (wantOn && !isOn)
            {
                if (EventManager.StartEvent(s.EventName, null!, null!))
                {
                    active[s.EventName] = true;
                    Broadcast($"EVENT '{s.EventName}' has started.");
                    ModManager.Log($"[EventClock] started '{s.EventName}' (scheduled)");
                }
            }
            else if (!wantOn && isOn)
            {
                if (EventManager.StopEvent(s.EventName, null!, null!))
                {
                    active[s.EventName] = false;
                    Broadcast($"EVENT '{s.EventName}' has ended.");
                    ModManager.Log($"[EventClock] stopped '{s.EventName}' (schedule window closed)");
                }
            }
        }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        active.Clear();
        var period = TimeSpan.FromSeconds(Math.Max(30, Cfg?.CheckSeconds ?? 30));
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[EventClock] {e.Message}"); }
        }, null, period, period);
        ModManager.Log($"[EventClock] ready, enabled={Cfg?.Enabled}, events={Cfg?.Events.Count ?? 0}");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    [CommandHandler("eventclock", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show the EventClock schedule and each event's state.", "[status]")]
    public static void HandleEventClock(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        Say(session, $"EventClock: enabled={c.Enabled}, checking every {Math.Max(30, c.CheckSeconds)}s, {c.Events.Count} scheduled event(s).");
        if (!c.Enabled)
        {
            Say(session, "Disabled (Settings.json Enabled=false) - use ACE's own /event command to start or stop events by hand.");
            return;
        }
        foreach (var s in c.Events)
        {
            var avail = !string.IsNullOrWhiteSpace(s.EventName) && EventManager.IsEventAvailable(s.EventName);
            var days = s.Days.Count == 0 ? "every day" : string.Join(",", s.Days);
            var state = avail ? EventManager.GetEventStatus(s.EventName).ToString() : "UNKNOWN (not in ace_world.events)";
            Say(session, $"  {s.EventName}: {days} {s.Start}-{s.End} -> state={state}");
        }
    }
}
