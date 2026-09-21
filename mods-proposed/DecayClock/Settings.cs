namespace DecayClock;

public class NexusSettings
{
    /// <summary>Cell id as a decimal number in JSON. 0 = no nexus.</summary>
    public uint Cell { get; set; } = 0;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 30f;
}

public class Settings
{
    public bool Enabled { get; set; } = false;
    public List<uint> MinionWcids { get; set; } = new() { 900021240 };
    /// <summary>Minutes of decay (away from the nexus, at multiplier 1) before the minion crumbles.</summary>
    public double DecayMinutesToDeath { get; set; } = 60;
    /// <summary>Cell 0 = no nexus, decay never halts.</summary>
    public NexusSettings Nexus { get; set; } = new();
    /// <summary>Decay speed in daylight (in-game time). 1.0 = off.</summary>
    public double DaylightMultiplier { get; set; } = 1.0;
    /// <summary>Warn the owner when this percent of life is used up.</summary>
    public List<int> WarnAtPercent { get; set; } = new() { 50, 80, 95 };
    /// <summary>Minimum 10.</summary>
    public int SweepSeconds { get; set; } = 15;
}
