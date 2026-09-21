namespace ServerPulse;

public class Settings
{
    /// <summary>Default number of busiest landblocks listed.</summary>
    public int TopN { get; set; } = 5;
    /// <summary>Upper limit for the /pulse argument.</summary>
    public int MaxTopN { get; set; } = 20;
}
