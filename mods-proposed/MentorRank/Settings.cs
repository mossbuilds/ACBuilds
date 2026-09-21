namespace MentorRank;

public class Settings
{
    /// <summary>Master switch. OFF by default.</summary>
    public bool Enabled { get; set; } = false;
    public int MinMentorLevel { get; set; } = 50;
    public int MaxApprenticeLevel { get; set; } = 30;
    /// <summary>Mentor must be at least this many levels above the apprentice.</summary>
    public int MinLevelGap { get; set; } = 20;
    public int MaxApprenticesPerMentor { get; set; } = 2;
    /// <summary>Both must be within this many game units (same landblock) to earn a reward.</summary>
    public double MaxDistance { get; set; } = 100;
    /// <summary>Also require the same fellowship (Player.Fellowship) for a reward.</summary>
    public bool RequireFellowship { get; set; } = true;
    public List<int> MilestoneLevels { get; set; } = new() { 10, 20, 30, 40, 50 };
    /// <summary>Pyreals per milestone. Default 0 = announcement only.</summary>
    public int RewardPyreals { get; set; } = 0;
    public int RewardSkillCredits { get; set; } = 0;
    public int MaxPyrealsPerReward { get; set; } = 5000;
    public int MaxCreditsPerReward { get; set; } = 1;
    /// <summary>Total rewards a mentor can receive per rolling hour.</summary>
    public int MaxRewardsPerMentorPerHour { get; set; } = 2;
    /// <summary>Hours before a player who left a pairing can pair again.</summary>
    public int RepairCooldownHours { get; set; } = 24;
    public int InviteTimeoutSeconds { get; set; } = 60;
    /// <summary>Own JSON keyed by character guid. Never the database.</summary>
    public string DataFile { get; set; } = "mentorrank-data.json";
}
