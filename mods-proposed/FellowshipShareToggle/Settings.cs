namespace FellowshipShareToggle;

public class Settings
{
    /// <summary>Master on/off switch, read once in OnWorldOpen. OFF by default.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Minimum seconds between successful toggles for the same fellowship (prevents on/off spam mid-fight).</summary>
    public int CooldownSeconds { get; set; } = 10;
}
