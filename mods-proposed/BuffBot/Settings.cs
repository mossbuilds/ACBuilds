namespace BuffBot;

public class Settings
{
    /// <summary>Master switch. Off by default: /buffme does nothing until set true.</summary>
    public bool Enabled { get; set; } = false;
    public int CooldownSeconds { get; set; } = 1800;
    /// <summary>SpellId enum names (ACE.Entity.Enum.SpellId). Level 6 self buffs = ordinary, time-limited.</summary>
    public List<string> Spells { get; set; } = new()
    {
        "StrengthSelf6", "EnduranceSelf6", "CoordinationSelf6",
        "QuicknessSelf6", "FocusSelf6", "WillpowerSelf6", "ArmorSelf6"
    };
}
