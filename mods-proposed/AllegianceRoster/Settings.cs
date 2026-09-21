namespace AllegianceRoster;

public class Settings
{
    /// <summary>Master on/off switch, read once in OnWorldOpen.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Max chat lines printed per page for "/roster all".</summary>
    public int MaxLines { get; set; } = 20;
    /// <summary>Include offline members in "/roster all" (level still shown; no last-seen).</summary>
    public bool ShowOffline { get; set; } = true;
}
