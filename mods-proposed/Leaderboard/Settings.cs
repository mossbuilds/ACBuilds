namespace Leaderboard;

public class Settings
{
    public bool Enabled { get; set; } = true;
    /// <summary>How many rows /top shows (capped at 25).</summary>
    public int Size { get; set; } = 10;
    /// <summary>Seconds the level ranking is cached (avoids scanning every character on each /top).</summary>
    public int CacheSeconds { get; set; } = 60;
    /// <summary>Kill tally file, relative to the server working dir (inside the mod folder if you set it so).</summary>
    public string DataFile { get; set; } = "leaderboard-kills.json";
}
