namespace MilestoneRewards;

public class Settings
{
    /// <summary>Master switch. OFF by default: nothing is granted until an admin turns it on.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Level -> skill credits granted on reaching it. Small on purpose.</summary>
    public Dictionary<int, int> LevelToCredits { get; set; } = new() { { 25, 1 }, { 50, 1 }, { 75, 1 }, { 100, 1 } };
    /// <summary>Hard cap on credits from any single milestone, whatever the map says.</summary>
    public int MaxCreditsPerMilestone { get; set; } = 3;
    /// <summary>Grant file (character guid -> levels already rewarded), relative to the server working dir. Own JSON, not the database.</summary>
    public string DataFile { get; set; } = "milestonerewards-granted.json";
}
