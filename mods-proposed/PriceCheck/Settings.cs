namespace PriceCheck;

public class Settings
{
    /// <summary>Master on/off switch, read once in OnWorldOpen. Off by default.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Assumed vendor BuyPrice multiplier (what a vendor pays a player), used only when no
    /// specific vendor is being asked about. Vendor.GetBuyCost() falls back to 1.0 (full Value)
    /// when a vendor's own BuyPrice property is unset, so 1.0 is the same "no markdown" default
    /// ACE itself uses - real vendors on this shard may set their own BuyPrice lower.
    /// </summary>
    public double AssumedBuyPriceMultiplier { get; set; } = 1.0;
}
