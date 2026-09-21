namespace MinionCleanup;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Only CombatPets with these weenie class ids are ever touched.</summary>
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>Owner farther than this (game units) from the minion, or on another landblock, destroys it.</summary>
    public float MaxDistance { get; set; } = 60f;
    /// <summary>Seconds between sweeps (minimum 5).</summary>
    public int SweepSeconds { get; set; } = 10;
    public bool OnLogout { get; set; } = true;
    public bool OnDeath { get; set; } = true;
    public bool OnTeleport { get; set; } = true;
}
