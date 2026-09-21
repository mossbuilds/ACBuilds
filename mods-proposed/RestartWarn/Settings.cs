namespace RestartWarn;

public class Settings
{
    /// <summary>Extra warning points, in seconds before shutdown (ACE's own notices still run).</summary>
    public List<int> WarnAtSeconds { get; set; } = new() { 180, 45, 20 };
    /// <summary>Extra text appended to each warning.</summary>
    public string Suffix { get; set; } = "Please find a safe spot and log out.";
}
