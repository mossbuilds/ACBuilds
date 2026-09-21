namespace StatCard;

public class Settings
{
    /// <summary>Default number of top skills listed.</summary>
    public int TopSkills { get; set; } = 5;
    /// <summary>Upper limit for the /statcard argument.</summary>
    public int MaxTopSkills { get; set; } = 15;
    /// <summary>Show the death count line.</summary>
    public bool ShowDeaths { get; set; } = true;
}
