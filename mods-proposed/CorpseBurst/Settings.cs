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

    /// <summary>
    /// Spell id that, when cast by a player, runs the corpse-burst ability instead of its stock effect
    /// (bound via WorldObject.HandleCastSpell). 0 = unbound. Recommended once confirmed player-castable
    /// with /spellinfo: 5544 "Nether Blast I" (Void Magic bolt/AoE family, usage 0 - the pick in
    /// docs/NECROMANCER_SPELLS.md for corpse_explosion). Ship at 0 until verified in game.
    /// </summary>
    public uint BurstSpellId { get; set; } = 0;

    /// <summary>Necromancer-only gate for BurstSpellId, checked via PathChoice's quest stamp (path_&lt;name&gt;).
    /// Empty string = no gate.</summary>
    public string RequirePath { get; set; } = "necromancer";

    /// <summary>Tom 2026-09-23: the bound spells cost mana only - no components needed or used up - when a necromancer
    /// (RequirePath) casts them. Anyone else still needs the spell's normal components.</summary>
    public bool FreeComponents { get; set; } = true;
}
