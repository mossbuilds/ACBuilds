namespace QuestFlagInspector;

public class Settings
{
    /// <summary>Master switch. Off by default, matching every other mod in this repo, even though this one is
    /// purely read-only.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Paginates a long registry so one /questflags call cannot flood chat.</summary>
    public int MaxLines { get; set; } = 40;
}
