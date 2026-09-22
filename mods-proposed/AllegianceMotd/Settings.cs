namespace AllegianceMotd;

public class Settings
{
    /// <summary>Master on/off switch, read once in OnWorldOpen.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Max characters allowed in a MOTD set via /amotd set.</summary>
    public int MaxLength { get; set; } = 200;
    /// <summary>Seconds to wait after entering the world before showing the MOTD (mirrors LoginGreeter; chat sent too early is lost).</summary>
    public double DelaySeconds { get; set; } = 4;
    /// <summary>Small JSON file, keyed by the allegiance monarch's character guid (stable allegiance identity). Never the DB.</summary>
    public string DataFile { get; set; } = "allegiancemotd-list.json";
}
