namespace CommandIndex;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>ALLOWLIST of command names that may be listed. Anything not named here is never shown.</summary>
    public List<string> Allowlist { get; set; } = new();
    /// <summary>Optional command name -> help text, used instead of the attribute Description.</summary>
    public Dictionary<string, string> DescriptionOverrides { get; set; } = new();
}
