namespace RentReminder;

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    public bool WarnAtLogin { get; set; } = true;
    /// <summary>Warn (login) only when rent is due in fewer than this many days. /rent always answers.</summary>
    public double WarnDays { get; set; } = 3;
    public double DelaySeconds { get; set; } = 5;
    /// <summary>Minimum minutes between reminders per player (login or /rent-triggered warnings).</summary>
    public double CooldownMinutes { get; set; } = 60;
    /// <summary>{0} = time remaining, {1} = due date UTC.</summary>
    public string DueMessage { get; set; } = "Your house rent is due in {0} (by {1} UTC). Visit your house and pay the maintenance before then.";
    public string OverdueMessage { get; set; } = "Your house rent is due now. Visit your house and pay the maintenance.";
    public string OkMessage { get; set; } = "Your house rent is due in {0} (by {1} UTC).";
    public string NoHouseMessage { get; set; } = "You do not own a house.";
}
