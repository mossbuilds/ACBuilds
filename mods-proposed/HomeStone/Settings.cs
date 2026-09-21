namespace HomeStone;

public class Settings
{
    public int CooldownSeconds { get; set; } = 300;
    public bool AllowIndoors { get; set; } = false;
    /// <summary>File (relative to the server working dir) holding saved spots.</summary>
    public string DataFile { get; set; } = "homestone-spots.json";
}
