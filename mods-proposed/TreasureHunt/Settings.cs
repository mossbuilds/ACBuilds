namespace TreasureHunt;

public class HuntDef
{
    public string Riddle { get; set; } = "";
    /// <summary>Cell id in hex, e.g. "A9B40001".</summary>
    public string Cell { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 10f;
    public int RewardPyreals { get; set; } = 1000;
}

public class Settings
{
    public bool Enabled { get; set; } = false;
    public int MaxMinutes { get; set; } = 30;
    public int CheckSeconds { get; set; } = 3;
    /// <summary>Hard cap on any reward, whatever a hunt asks for.</summary>
    public int MaxRewardPyreals { get; set; } = 5000;
    public List<HuntDef> Hunts { get; set; } = new()
    {
        new HuntDef { Riddle = "Example riddle - edit me.", Cell = "A9B40001", X = 84f, Y = 7.7f, Z = 94f, Radius = 10f, RewardPyreals = 1000 }
    };
}
