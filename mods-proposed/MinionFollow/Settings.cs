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
    /// <summary>Seconds between follow checks. ACE's own passive-pet SlowTick runs every 1 second; keep this close to that.</summary>
    public double CheckSeconds { get; set; } = 1.0;
}
