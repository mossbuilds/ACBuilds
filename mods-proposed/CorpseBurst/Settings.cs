namespace CorpseBurst;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    public int CooldownSeconds { get; set; } = 20;
    /// <summary>SpellId enum name (ACE.Entity.Enum.SpellId), a projectile ring/arc spell.</summary>
    public string SpellId { get; set; } = "FlameRing";
    /// <summary>How far from the player a corpse may be.</summary>
    public float Radius { get; set; } = 10f;
    /// <summary>A living monster must be within this of the corpse to be aimed at.</summary>
    public float BurstRadius { get; set; } = 8f;
    public int ManaCost { get; set; } = 0;
    public int CorpseMaxAgeSec { get; set; } = 0;
}
