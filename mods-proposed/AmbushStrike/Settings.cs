namespace AmbushStrike;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Damage multiplier on an ambush hit (clamped 1..5).</summary>
    public double Multiplier { get; set; } = 2.0;
    /// <summary>Only when the attacker is behind the target (angle > 90).</summary>
    public bool RequireBehind { get; set; } = false;
    /// <summary>Only when the creature has no attack target yet (or is asleep).</summary>
    public bool OnlyUnaware { get; set; } = true;
    /// <summary>Minimum seconds between ambush bonuses on the same target by the same attacker. 0 = none.</summary>
    public double CooldownSecondsPerTarget { get; set; } = 0;
    /// <summary>If set, attacker needs this quest stamped (e.g. "path_rogue"). Empty = everyone.</summary>
    public string RequirePathQuest { get; set; } = "";
}
