namespace PkNight;

public class Settings
{
    /// <summary>Master switch. Off by default: nothing is announced or changed while false.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Days the window runs (server local time), e.g. "Friday".</summary>
    public List<string> Days { get; set; } = new() { "Friday" };
    /// <summary>Window start, HH:mm, server local time.</summary>
    public string Start { get; set; } = "20:00";
    /// <summary>Window end, HH:mm, server local time (may be earlier than Start to cross midnight).</summary>
    public string End { get; set; } = "22:00";
    /// <summary>Minutes before the window to announce it (0 = no advance warning).</summary>
    public int WarnMinutes { get; set; } = 10;
    /// <summary>Landblocks (hex) that stay safe even during PK night, e.g. tutorial or town areas.</summary>
    public List<string> SafeLandblocks { get; set; } = new();
}
