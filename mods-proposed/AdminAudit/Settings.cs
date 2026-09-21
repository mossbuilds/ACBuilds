namespace AdminAudit;

public class Settings
{
    public bool Enabled { get; set; } = true;
    /// <summary>Audit log file, relative to the server working dir (put it inside the mod folder if you prefer).</summary>
    public string LogFile { get; set; } = "adminaudit.log";
    /// <summary>Lowest access level of a command that gets logged (Advocate and up by default; use Player to log everything).</summary>
    public AccessLevel MinLevel { get; set; } = AccessLevel.Advocate;
    /// <summary>Commands whose arguments are never written (only the command name).</summary>
    public List<string> RedactArgs { get; set; } = new() { "passwd", "acpassword", "accountcreate", "accountpassword", "password" };
}
