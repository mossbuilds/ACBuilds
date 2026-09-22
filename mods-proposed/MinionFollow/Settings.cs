namespace MinionFollow;

public class Settings
{
    /// <summary>Master switch. OFF by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Only CombatPets with these weenie class ids are ever made to follow (default: RaiseSkeleton's skeleton minion).</summary>
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>Below this distance from the owner, the minion does not bother moving (matches ACE's own passive-pet MinDistance).</summary>
    public float MinDistance { get; set; } = 4f;
    /// <summary>Beyond this distance the minion is destroyed instead of endlessly chasing (matches MinionCleanup's default so the two agree).</summary>
    public float MaxDistance { get; set; } = 60f;
    /// <summary>Seconds between ticks. ACE's own passive-pet Tick() runs 5x/second (0.2s) to actually progress a walk in
    /// small steps - a slower interval here just makes a following minion look laggy/jerky, it doesn't save meaningful work.</summary>
    public double CheckSeconds { get; set; } = 0.2;
}
