namespace HouseOverpayRefund;

public class Settings
{
    /// <summary>Off by default (Tom's rule: house-money code ships off until proven on a test server).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Broadcast a system-chat line to the payer when a refund is minted.</summary>
    public bool NotifyPlayer { get; set; } = true;

    /// <summary>Log every refund (guid, amount, buy/rent) to the server log.</summary>
    public bool LogRefunds { get; set; } = true;

    public string RefundMessage { get; set; } = "Refunded {0} pyreals - your payment covered more than was owed.";
}
