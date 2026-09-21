namespace TeleBack;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Lowest AccessLevel recorded and allowed to use /teleback.</summary>
    public AccessLevel MinAccess { get; set; } = AccessLevel.Sentinel;
    public int HistorySize { get; set; } = 5;
}
