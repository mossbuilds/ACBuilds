namespace MinionOrders;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Only CombatPets with these weenie class ids obey orders.</summary>
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>Minimum milliseconds between /order uses per player.</summary>
    public int OrderCooldownMs { get; set; } = 1500;

    /// <summary>
    /// Spell id that, when cast by a player, orders their minions to attack instead of running its stock
    /// effect. The cast's own target is used when it resolves to a hostile Creature; otherwise falls back to
    /// the player's selected creature (HealthQueryTarget), same as /order attack. 0 = unbound. Recommended
    /// once confirmed player-castable with /spellinfo: 5332 "Bael'zharon's Nether Streak" (Void Magic, usage 0).
    /// Ship at 0 until verified in game.
    /// </summary>
    public uint AttackSpellId { get; set; } = 0;

    /// <summary>Spell id bound to /order hold. 0 = unbound. Recommended: 4194 "Magical Void" (usage 0).</summary>
    public uint HoldSpellId { get; set; } = 0;

    /// <summary>Spell id bound to /order follow. 0 = unbound. Recommended: 3235 "Dark Power" (usage 0).</summary>
    public uint FollowSpellId { get; set; } = 0;

    /// <summary>Necromancer-only gate for the three spell ids above, checked via PathChoice's quest stamp
    /// (path_&lt;name&gt;). Empty string = no gate.</summary>
    public string RequirePath { get; set; } = "necromancer";
}
