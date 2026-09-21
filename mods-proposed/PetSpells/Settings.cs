namespace PetSpells;

public class Settings
{
    public bool Enabled { get; set; } = false;
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>SpellId enum name cast at the minion's AttackTarget (debuff/curse). Empty = none.</summary>
    public string SpellOnTarget { get; set; } = "";
    /// <summary>SpellId enum name cast on the owner (small heal/buff). Empty = none.</summary>
    public string SpellOnOwner { get; set; } = "";
    public bool Debuff { get; set; } = true;
    public bool Buff { get; set; } = true;
    /// <summary>Minimum 5, enforced.</summary>
    public int CastIntervalSeconds { get; set; } = 10;
    public float MaxRange { get; set; } = 30f;
    public bool ManaFree { get; set; } = true;
    public int ManaCost { get; set; } = 0;
    public int MaxCastsPerSecondPerOwner { get; set; } = 1;
}
