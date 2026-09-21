namespace LoginShield;

public class Settings
{
    public bool Enabled { get; set; } = false;
    /// <summary>Seconds of damage immunity after entering the world.</summary>
    public double ShieldSeconds { get; set; } = 10;
    /// <summary>End the shield on the player's first melee, missile or targeted spell attack.</summary>
    public bool BreakOnAttack { get; set; } = true;
    /// <summary>Tell the player when the shield starts.</summary>
    public bool Notify { get; set; } = true;
    public string StartMessage { get; set; } = "You are protected for {0} seconds, or until you attack.";
}
