namespace XpBoost;

public class Settings
{
    /// <summary>XP multiplier applied to earned XP. 1.0 = off.</summary>
    public double Multiplier { get; set; } = 1.0;
    /// <summary>Only players at or below this level are boosted (0 = no limit).</summary>
    public int MaxLevel { get; set; } = 0;
    /// <summary>XpType names to boost (Kill, Quest, Allegiance, ...).</summary>
    public List<string> XpTypes { get; set; } = new() { "Kill" };
}
