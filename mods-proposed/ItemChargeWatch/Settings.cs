namespace ItemChargeWatch;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Item is flagged as low when its remaining mana percent is at or under this value.</summary>
    public int WarnPercent { get; set; } = 20;
}
