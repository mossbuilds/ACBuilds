namespace AgentDialogue;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Watched NPC weenie class IDs. Empty = nothing is watched, nothing is patched into action. Admin opt-in only.</summary>
    public List<uint> WatchedWcids { get; set; } = new();

    /// <summary>Inbox file (one JSON object per line), relative to the server working dir. Directories are created as needed.</summary>
    public string InboxFile { get; set; } = "agent/dialogue/inbox.jsonl";

    /// <summary>Outbox file AgentDialogue polls for replies, relative to the server working dir.</summary>
    public string OutboxFile { get; set; } = "agent/dialogue/outbox.jsonl";

    /// <summary>How often to poll OutboxFile. Clamped to at least 1 second.</summary>
    public int PollSeconds { get; set; } = 2;

    /// <summary>Hard cap on any text written to inbox.jsonl or spoken from outbox.jsonl. Control characters are stripped first.</summary>
    public int MaxLength { get; set; } = 500;

    /// <summary>Outbox lines whose npc_wcid has no currently-loaded instance are retried on the next poll, up to this many
    /// polls, then dropped (logged) so a stale line can't grow the file forever while an NPC is unloaded.</summary>
    public int MaxRetryPolls { get; set; } = 30;
}
