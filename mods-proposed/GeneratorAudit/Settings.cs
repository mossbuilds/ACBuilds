namespace GeneratorAudit;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>How long (seconds) a generator must show AllProfilesUnavailable at every scan, with no gap, before it's flagged as stalled (minimum 30 enforced), matching StuckVendorWatch/HotspotAlert's cadence conventions.</summary>
    public int ScanSeconds { get; set; } = 60;
}
