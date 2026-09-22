namespace EventClock;

public class ScheduledEvent
{
    /// <summary>Name of an event already defined in ace_world's `events` table (must pass EventManager.IsEventAvailable).</summary>
    public string EventName { get; set; } = "";
    /// <summary>Days the window runs (server local time), e.g. "Friday". Empty = every day.</summary>
    public List<string> Days { get; set; } = new();
    /// <summary>Window start, HH:mm, server local time.</summary>
    public string Start { get; set; } = "00:00";
    /// <summary>Window end, HH:mm, server local time (may be earlier than Start to cross midnight).</summary>
    public string End { get; set; } = "00:00";
}

public class Settings
{
    /// <summary>Master switch. Off by default: nothing is started, stopped or checked while false.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>How often the schedule is checked, in seconds. Minimum 30 regardless of a lower value here.</summary>
    public int CheckSeconds { get; set; } = 30;
    /// <summary>Events to schedule. Each EventName must already exist in ace_world (see IsEventAvailable in /eventclock status).</summary>
    public List<ScheduledEvent> Events { get; set; } = new();
}
