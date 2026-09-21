namespace GentleDeath;

public class Settings
{
    /// <summary>Max items dropped on death (-1 = no cap, 0 = drop none).</summary>
    public int MaxItemsDropped { get; set; } = 2;
    /// <summary>Vitae penalty percent per death (ACE default 5; minimum applied is 1).</summary>
    public int VitaeAmount { get; set; } = 2;
    /// <summary>If true, deaths caused by another player (PK) are left vanilla.</summary>
    public bool SkipPkDeaths { get; set; } = true;
}
