namespace ZoneSweep;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Seconds a preview stays valid for a follow-up "confirm".</summary>
    public int ConfirmSeconds { get; set; } = 60;
    /// <summary>Seconds between the in-chat warning and the actual logoff.</summary>
    public int WarnSeconds { get; set; } = 8;
    public string WarnMessage { get; set; } = "This zone is being cleared by staff. You will be logged off in {0} seconds.";
    public string LogFile { get; set; } = "zonesweep.log";
    /// <summary>Rotate when the file exceeds this many KB.</summary>
    public int MaxKb { get; set; } = 512;
    /// <summary>Rotated files kept (zonesweep.log.1 .. .N).</summary>
    public int MaxFiles { get; set; } = 5;
}
