namespace SettingsPeek;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>ALLOWLIST of ACE bool property keys that may be shown. Nothing outside these lists is ever read.</summary>
    public List<string> BoolKeys { get; set; } = new();
    public List<string> LongKeys { get; set; } = new();
    public List<string> DoubleKeys { get; set; } = new();
}
