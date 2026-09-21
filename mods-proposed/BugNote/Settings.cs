namespace BugNote;

public class Settings
{
    public int CooldownSeconds { get; set; } = 60;
    public int MaxLength { get; set; } = 200;
    public int MaxNotes { get; set; } = 500;
    /// <summary>Small JSON file inside the mod folder (no database access).</summary>
    public string DataFile { get; set; } = "bugnotes.json";
}
