namespace ThreatTableView;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Maximum number of ranked damagers printed by /threattable.</summary>
    public int MaxDamagers { get; set; } = 10;
}
