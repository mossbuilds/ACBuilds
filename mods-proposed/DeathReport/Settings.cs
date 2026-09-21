namespace DeathReport;

public class Settings
{
    public bool Enabled { get; set; } = true;
    public bool Broadcast { get; set; } = true;
    /// <summary>Minimum seconds between world broadcasts (anti-spam).</summary>
    public int BroadcastCooldownSeconds { get; set; } = 30;
    public bool AllowLastDeath { get; set; } = true;
    /// <summary>Per-player cooldown for /lastdeath.</summary>
    public int LastDeathCooldownSeconds { get; set; } = 300;
    public string LogFile { get; set; } = "deaths.log";
    public List<string> Messages { get; set; } = new()
    {
        "{0} has met an untimely end.",
        "{0} has fallen. Send help, and a corpse run.",
        "{0} went down swinging. Mostly down.",
        "Another one bites the dust: {0}."
    };
}
