namespace LoginGreeter;

public class Settings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Seconds to wait after entering the world before sending (chat sent too early is lost).</summary>
    public double DelaySeconds { get; set; } = 3;
    /// <summary>{0} = character name.</summary>
    public string FirstLoginMessage { get; set; } = "Welcome to the server, {0}! Type /rules to see the server rules.";
    public string ReturningMessage { get; set; } = "Welcome back, {0}.";
    /// <summary>{0} = players online. Empty string disables.</summary>
    public string OnlineMessage { get; set; } = "Players online: {0}.";
    /// <summary>{0} = character name. Sent to the first-login player only.</summary>
    public List<string> FirstLoginTips { get; set; } = new() { "Tip: type @acehelp for the list of commands." };
    /// <summary>Lines shown by /rules ({0} = character name).</summary>
    public List<string> Rules { get; set; } = new() { "1. Be respectful to other players.", "2. No exploiting bugs.", "3. Have fun, {0}!" };
}
