namespace TimedMute;

public class Settings
{
    public int DefaultMinutes { get; set; } = 10;
    public int MaxMinutes { get; set; } = 1440;
    /// <summary>Small JSON file for the mute list (no database access).</summary>
    public string DataFile { get; set; } = "timedmute-list.json";
}
