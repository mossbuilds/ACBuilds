namespace AgentActions;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>wcids that `spawn` is allowed to create. Empty = nothing is spawnable. Admin opt-in only.
    /// Checked in addition to (never instead of) the hard WeenieType.Admin/Sentinel/Undef refusal below.</summary>
    public List<uint> AllowedSpawnWcids { get; set; } = new();

    /// <summary>Hard cap on any text in a speak/broadcast request. Control characters are stripped first.</summary>
    public int MaxTextLength { get; set; } = 500;

    /// <summary>Rolling hourly cap on accepted `broadcast` requests, since a broadcast reaches every player.</summary>
    public int MaxBroadcastsPerHour { get; set; } = 6;

    /// <summary>How often to poll InboxFile. Clamped to at least 1 second.</summary>
    public int PollSeconds { get; set; } = 2;

    /// <summary>Inbox file (one JSON object per line), relative to the server working dir. Directories are created as needed.
    /// Consumed lines are removed after each poll (rewrite under lock).</summary>
    public string InboxFile { get; set; } = "agent/actions/inbox.jsonl";

    /// <summary>Outcome log (accepted+result, or refused+reason), one JSON object appended per request, relative to the
    /// server working dir. Never truncated or rewritten by this mod - it is an append-only audit trail.</summary>
    public string LogFile { get; set; } = "agent/actions/log.jsonl";
}
