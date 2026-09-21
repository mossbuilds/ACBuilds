namespace TradeLedger;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Trade log file, relative to the server working dir.</summary>
    public string LogFile { get; set; } = "tradeledger.log";
    /// <summary>Rotate when the file exceeds this many KB.</summary>
    public int MaxKb { get; set; } = 512;
    /// <summary>Rotated files kept (tradeledger.log.1 .. .N).</summary>
    public int MaxFiles { get; set; } = 5;
    public bool LogItems { get; set; } = true;
    public bool LogCoins { get; set; } = true;
}
