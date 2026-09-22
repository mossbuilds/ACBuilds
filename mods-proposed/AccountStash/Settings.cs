namespace AccountStash;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Path to the mod-owned JSON balance file (Account -> pyreal balance). No ace_auth/ace_shard access.</summary>
    public string BalanceFile { get; set; } = "AccountStash_balances.json";

    /// <summary>Highest balance a single account may hold.</summary>
    public long MaxBalance { get; set; } = 250_000_000;

    /// <summary>Minimum seconds between deposits/withdrawals for the same account.</summary>
    public double CooldownSeconds { get; set; } = 5;

    public string DepositOk { get; set; } = "Deposited {0} pyreals. Account stash balance: {1}.";
    public string WithdrawOk { get; set; } = "Withdrew {0} pyreals. Account stash balance: {1}.";
    public string BalanceMessage { get; set; } = "Account stash balance: {0} pyreals.";
    public string NotEnoughInventory { get; set; } = "You do not have that many pyreals to deposit.";
    public string NotEnoughStash { get; set; } = "The account stash does not have that many pyreals.";
    public string OverMax { get; set; } = "That deposit would put the account stash over its {0} pyreal limit.";
    public string BadAmount { get; set; } = "Usage: /stash deposit <amount> | /stash withdraw <amount> | /stash balance";
    public string OnCooldown { get; set; } = "The account stash is busy with another transaction for this account. Try again in a moment.";
    public string InventoryFull { get; set; } = "Withdrawal failed: your inventory could not hold the pyreals (pack full?). Nothing was taken from the stash.";
}
