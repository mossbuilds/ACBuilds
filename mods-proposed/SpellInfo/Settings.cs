namespace SpellInfo;

public class Settings
{
    /// <summary>Master switch. Off by default, like the other proposed mods.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Cap on how many spell ids /spellinfo find will list.</summary>
    public int FindLimit { get; set; } = 20;
}
