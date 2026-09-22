namespace StuckVendorWatch;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Seconds between scans (minimum 30 enforced), matching HotspotAlert's cadence.</summary>
    public int ScanSeconds { get; set; } = 60;
}
