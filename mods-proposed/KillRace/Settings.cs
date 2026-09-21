namespace KillRace;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Minutes between standings broadcasts during a race.</summary>
    public int StandingsMinutes { get; set; } = 5;
    public int MaxRaceMinutes { get; set; } = 120;
    /// <summary>Pyreals paid to each winner (ties all get it). 0 = no reward.</summary>
    public int RewardPyreals { get; set; } = 5000;
    /// <summary>Hard cap on the reward, whatever RewardPyreals says.</summary>
    public int RewardCap { get; set; } = 20000;
    public int Size { get; set; } = 5;
}
