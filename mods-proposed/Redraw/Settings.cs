namespace Redraw;

public class Settings
{
    public bool Enabled { get; set; } = false;
    public int CooldownSeconds { get; set; } = 30;
    /// <summary>0 = no hourly cap.</summary>
    public int MaxPerHour { get; set; } = 20;
    /// <summary>Safety cap on objects re-sent per use.</summary>
    public int MaxObjects { get; set; } = 300;
}
