namespace CorpseWatch;

public class Settings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Whisper the owner when this many seconds (or fewer) remain before decay. Checked in descending order; each fires once per corpse.</summary>
    public List<int> WarnSeconds { get; set; } = new() { 300, 60 };
    /// <summary>How often the sweep checks tracked corpses, in seconds.</summary>
    public int SweepSeconds { get; set; } = 15;
    /// <summary>Oldest tracked corpses are dropped past this count (defends against a mass-death event).</summary>
    public int MaxTracked { get; set; } = 200;
    public bool AllowMyCorpse { get; set; } = true;
}
