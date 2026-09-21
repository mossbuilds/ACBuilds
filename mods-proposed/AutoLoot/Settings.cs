namespace AutoLoot;

public class Settings
{
    public bool Coins { get; set; } = true;
    public bool TradeNotes { get; set; } = true;
    public bool Gems { get; set; } = false;
    /// <summary>Close the corpse window after looting so the client refreshes its view.</summary>
    public bool CloseCorpse { get; set; } = true;
}
