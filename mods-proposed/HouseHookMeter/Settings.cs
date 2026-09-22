namespace HouseHookMeter;

public class Settings
{
    /// <summary>Master on/off switch, read once in OnWorldOpen. Read-only mod, off by default for consistency
    /// with every other mod here even though nothing it does requires the gate.</summary>
    public bool Enabled { get; set; } = false;
}
