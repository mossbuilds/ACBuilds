namespace AnnounceEvents;

public class Settings
{
    /// <summary>Seconds between scheduled broadcasts.</summary>
    public int IntervalSeconds { get; set; } = 900;
    /// <summary>Messages cycled in order. Empty list disables scheduling.</summary>
    public List<string> Messages { get; set; } = new() { "Welcome to ACBuilds! Have fun and be kind." };
}
