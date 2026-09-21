namespace IdleKick;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Minutes without movement input before the warning.</summary>
    public int IdleMinutes { get; set; } = 60;
    /// <summary>Seconds between the warning and the log off.</summary>
    public int GraceSeconds { get; set; } = 120;
    public string WarnMessage { get; set; } = "You appear to be away. Move or act within {0} seconds or you will be logged off.";
}
