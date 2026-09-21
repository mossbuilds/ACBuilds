namespace MinionOrders;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Only CombatPets with these weenie class ids obey orders.</summary>
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>Minimum milliseconds between /order uses per player.</summary>
    public int OrderCooldownMs { get; set; } = 1500;
}
