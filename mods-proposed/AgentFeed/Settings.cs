namespace AgentFeed;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Event log file, relative to the server working dir. Matches the shared-contract path (docs/AI_AGENT_HOOKS_PLAN.md).</summary>
    public string LogFile { get; set; } = "agent/feed/events.log";

    /// <summary>Rotate when the file exceeds this many KB.</summary>
    public int MaxKb { get; set; } = 512;

    /// <summary>Rotated files kept (events.log.1 .. .N).</summary>
    public int MaxFiles { get; set; } = 5;

    public bool LogLogin { get; set; } = true;
    public bool LogLogout { get; set; } = true;
    public bool LogDeath { get; set; } = true;

    /// <summary>Optional outbound webhook. Empty = off. POSTs a small JSON body of the same event, fire-and-forget.
    /// This is the only mod in this suite that makes an outbound network call.</summary>
    public string WebhookUrl { get; set; } = "";

    /// <summary>Timeout for the webhook POST; the request is fire-and-forget regardless (never blocks the game thread).</summary>
    public int WebhookTimeoutSeconds { get; set; } = 5;
}
