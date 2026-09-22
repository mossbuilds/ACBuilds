namespace AgentBridge;

public class Settings
{
    public bool Enabled { get; set; } = false;

    /// <summary>Loopback port to listen on. Only used if Enabled and SharedSecret are both set. Default chosen to be
    /// unlikely to collide with anything else on the box; change it if it does.</summary>
    public int Port { get; set; } = 8523;

    /// <summary>Shared secret every request must send as the X-Agent-Secret header, compared with plain string
    /// equality (NOT constant-time - see README; this mod is a private-server convenience tool, not meant to face
    /// the internet). Empty/whitespace means "refuse to start" - this mod never runs unauthenticated.</summary>
    public string SharedSecret { get; set; } = "";

    /// <summary>Wcids to report on in GET /state: for each, whether a loaded instance exists and its landblock.
    /// Empty = the "watched" section of /state is just an empty list. Independent of AgentDialogue's own
    /// WatchedWcids/AgentActions' AllowedSpawnWcids - set separately here.</summary>
    public List<uint> WatchWcids { get; set; } = new();

    /// <summary>Same outbox file AgentDialogue polls. POST /dialogue/{wcid}/reply appends a line here in the exact
    /// shape AgentDialogue's PatchClass.PollOutbox already parses: {"npc_wcid":<uint>,"text":"..."}.</summary>
    public string DialogueOutboxFile { get; set; } = "agent/dialogue/outbox.jsonl";

    /// <summary>Same inbox file AgentActions polls. POST /actions appends the request body here verbatim (after
    /// validating only that it is well-formed JSON) - AgentActions does the real validation when it next polls.</summary>
    public string ActionsInboxFile { get; set; } = "agent/actions/inbox.jsonl";

    /// <summary>Hard cap on request body bytes read from any POST, to keep a huge/pathological body from being a
    /// trivial memory hog. Anything larger is refused with 400 before being fully read.</summary>
    public int MaxBodyBytes { get; set; } = 8192;

    /// <summary>Hard cap on the "text" field length in a dialogue reply, mirroring AgentDialogue's own MaxLength
    /// default. AgentBridge does not otherwise touch the text - AgentDialogue re-cleans/re-caps it again on its side.</summary>
    public int MaxReplyTextLength { get; set; } = 500;
}
