namespace PkGuard;

public class Settings
{
    /// <summary>Landblocks (hex, upper 16 bits of the cell id, e.g. "A9B4") where player-vs-player damage is blocked.</summary>
    public List<string> Landblocks { get; set; } = new();
}
