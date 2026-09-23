namespace RaiseSkeleton;

public class Settings
{
    /// <summary>Master switch. Default false: the SQL weenies must be applied first.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>CombatPet weenie for the controlled minion (sql/900021240_raiseskelminion.sql).</summary>
    public uint MinionWcid { get; set; } = 900021240;
    /// <summary>Hostile creature weenie for the feral skeleton (sql/900021241_raiseskelferal.sql).</summary>
    public uint FeralWcid { get; set; } = 900021241;
    /// <summary>Retail PetDevice weenie used only as a transient object handed to CombatPet.Init (never enters the world). 48942 = Fire Skeleton Minion Essence (50).</summary>
    public uint DeviceWcid { get; set; } = 48942;
    /// <summary>Max distance from the caller to the corpse, in game units.</summary>
    public float Radius { get; set; } = 6f;
    /// <summary>Self points per controlled minion: limit = floor(Self / Divisor) + BonusLimit.</summary>
    public int Divisor { get; set; } = 10;
    /// <summary>Flat bonus added to the limit.</summary>
    public int BonusLimit { get; set; } = 0;
    /// <summary>The limit never exceeds this.</summary>
    public int MaxMinionsHardCap { get; set; } = 10;
    /// <summary>Mana spent per /raiseskel (0 = free).</summary>
    public int ManaCost { get; set; } = 0;
    /// <summary>Seconds between /raiseskel uses per player.</summary>
    public int CooldownSeconds { get; set; } = 2;
    /// <summary>Minutes before a controlled minion despawns (0 = never).</summary>
    public int MinionMinutes { get; set; } = 30;
    /// <summary>Only corpses whose Level is at least this can be raised (0 = any; a corpse with no Level is refused when this is above 0).</summary>
    public int MinCorpseLevel { get; set; } = 0;

    /// <summary>
    /// Spell id that, when cast by a player, runs RaiseFromNearestCorpse instead of its stock effect
    /// (HandleCastSpell prefix). 0 = unbound (no spell triggers this). Recommended once confirmed
    /// player-castable with /spellinfo: 3801 "Shadow Touch" (Void Magic, usage 0 - see docs/NECROMANCER_SPELLS.md).
    /// Ship this at 0 until /spellinfo 3801 -> /addspell 3801 -> cast has been checked in game.
    /// </summary>
    public uint RaiseSpellId { get; set; } = 0;

    /// <summary>
    /// Spell id that, when cast by a player, runs DismissAll instead of its stock effect. 0 = unbound.
    /// Recommended once confirmed player-castable: 3803 "Shadow Shot" (Void Magic, usage 0). Distinct from
    /// RaiseSpellId and from every other bound id in MinionOrders/CorpseBurst. Ship at 0 until verified.
    /// </summary>
    public uint DismissSpellId { get; set; } = 0;

    /// <summary>Necromancer-only gate for RaiseSpellId/DismissSpellId, checked via PathChoice's quest stamp
    /// (QuestPrefix + this name, default "path_necromancer"). Empty string = no gate (anyone who has the
    /// spell can cast it).</summary>
    public string RequirePath { get; set; } = "necromancer";

    /// <summary>Tom 2026-09-23: the bound spells cost mana only - no components needed or used up - when a necromancer
    /// (RequirePath) casts them. Anyone else still needs the spell's normal components.</summary>
    public bool FreeComponents { get; set; } = true;
}
