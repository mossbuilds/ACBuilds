namespace RecallCooldown;

public class Settings
{
    /// <summary>Master switch. Off by default: the prefix returns true immediately (retail behaviour) while false.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Shared cooldown in seconds across every SpellType.PortalRecall spell. 0 = no gating even when Enabled.</summary>
    public int CooldownSeconds { get; set; } = 0;
    /// <summary>If true, players whose PlayerKillerStatus is PK are never gated.</summary>
    public bool ExemptInPk { get; set; } = false;
    /// <summary>Seconds between sweeps that drop expired cooldown entries so the dictionary cannot grow unbounded.</summary>
    public int PruneSeconds { get; set; } = 300;
}
