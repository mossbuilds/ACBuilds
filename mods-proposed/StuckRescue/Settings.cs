namespace StuckRescue;

public class Settings
{
    public bool Enabled { get; set; } = false;
    public int CooldownSeconds { get; set; } = 300;
    /// <summary>0 = no hourly cap.</summary>
    public int MaxPerHour { get; set; } = 0;
    public float MaxDistance { get; set; } = 30f;
    public float StepDistance { get; set; } = 3f;
    public float ZOffset { get; set; } = 0.5f;
    public int MinSecondsSinceTeleport { get; set; } = 10;
    /// <summary>When nudging fails or the player is indoors: recall to the ACE sanctuary (lifestone).</summary>
    public bool FallbackToSanctuary { get; set; } = true;
}
