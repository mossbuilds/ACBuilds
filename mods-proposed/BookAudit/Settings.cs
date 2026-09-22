namespace BookAudit;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Book-write audit log file, relative to the server working dir.</summary>
    public string LogFile { get; set; } = "bookaudit.log";
    /// <summary>Rotate when the file exceeds this many KB.</summary>
    public int MaxKb { get; set; } = 512;
    /// <summary>Rotated files kept (bookaudit.log.1 .. .N).</summary>
    public int MaxFiles { get; set; } = 5;
    /// <summary>When false, logs only action + length, not the written page text itself.</summary>
    public bool LogFullText { get; set; } = true;
}
