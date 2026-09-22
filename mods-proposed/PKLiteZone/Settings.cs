namespace PKLiteZone;

public class Settings
{
    /// <summary>Master switch. Off by default: nothing is checked or changed while false.</summary>
    public bool Enabled { get; set; } = false;
    /// <summary>Landblocks (hex, e.g. "0033") where an opted-in player is switched to PKLite. Empty = no zone, /pklite on has no effect anywhere.</summary>
    public List<string> ZoneLandblocks { get; set; } = new();
    /// <summary>If true, a player who logs out while PKLite (via this mod) is reverted to NPK before the character saves.</summary>
    public bool RevertOnLogout { get; set; } = true;
    /// <summary>Seconds between sweeps that apply/revert PKLite for opted-in online players based on their current landblock.</summary>
    public int SweepSeconds { get; set; } = 5;
}
