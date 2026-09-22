namespace TickLagAlert;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Seconds. UpdateGameWorld_Entire's LastEvent above this counts as a slow tick.</summary>
    public double WarnSeconds { get; set; } = 0.5;
    /// <summary>Consecutive slow-tick checks required before alerting, to avoid firing on a single GC pause.</summary>
    public int SustainedTicks { get; set; } = 3;
    /// <summary>Seconds between polls of the monitor.</summary>
    public int PollSeconds { get; set; } = 10;
    /// <summary>Minutes before the same sustained-lag condition alerts again.</summary>
    public int AlertCooldownMinutes { get; set; } = 10;
}
