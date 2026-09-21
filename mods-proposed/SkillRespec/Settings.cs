namespace SkillRespec;

public class Settings
{
    /// <summary>Master switch. OFF by default; the command does nothing until true.</summary>
    public bool Enabled { get; set; } = false;
    public int CooldownHours { get; set; } = 168;
    /// <summary>Pyreals charged per respec (0 = free).</summary>
    public int CostPyreals { get; set; } = 0;
    /// <summary>Seconds the player has to repeat the command / add confirm.</summary>
    public int ConfirmSeconds { get; set; } = 30;
    /// <summary>Small JSON file of last-use times (no database access).</summary>
    public string DataFile { get; set; } = "skillrespec-uses.json";
}
