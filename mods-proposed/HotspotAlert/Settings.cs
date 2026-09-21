namespace HotspotAlert;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    public int CreatureThreshold { get; set; } = 150;
    public int PlayerThreshold { get; set; } = 40;
    /// <summary>Seconds between scans (minimum 30 enforced).</summary>
    public int ScanSeconds { get; set; } = 60;
    /// <summary>Minutes before the same landblock alerts again.</summary>
    public int AlertCooldownMinutes { get; set; } = 10;
}
