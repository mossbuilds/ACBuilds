namespace LootWatch;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Loot audit log file, relative to the server working dir.</summary>
    public string LogFile { get; set; } = "lootwatch.log";
    /// <summary>Rotate when the file exceeds this many KB.</summary>
    public int MaxKb { get; set; } = 512;
    /// <summary>Rotated files kept (lootwatch.log.1 .. .N).</summary>
    public int MaxFiles { get; set; } = 5;
}
