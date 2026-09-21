namespace ModTest;

public class Settings
{
    /// <summary>Master switch. Off by default.</summary>
    public bool Enabled { get; set; } = false;
    public List<string> ExpectedMods { get; set; } = new();
    /// <summary>Mod name -> command names it should register. MinionOrders registers `order` (verified: not an ACE built-in).</summary>
    public Dictionary<string, List<string>> ExpectedCommands { get; set; } = new();
    public List<uint> RequiredWeenies { get; set; } = new();
    public List<string> BoolProperties { get; set; } = new();
}
