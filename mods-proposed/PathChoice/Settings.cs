namespace PathChoice;

public class PathDef
{
    /// <summary>Lower-case id; the ACE quest name is QuestPrefix + Name (e.g. path_necromancer).</summary>
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Optional CharacterTitle enum id granted on choice (0 = none). Must be an existing id.</summary>
    public uint TitleId { get; set; } = 0;
}

public class Settings
{
    public bool Enabled { get; set; } = false;
    public string QuestPrefix { get; set; } = "path_";
    public bool AllowChange { get; set; } = false;
    public int ChangeCooldownHours { get; set; } = 24;
    public List<PathDef> Paths { get; set; } =
    [
        new() { Name = "necromancer", Title = "Necromancer", Description = "Raise the dead and bind minions." },
        new() { Name = "rogue", Title = "Rogue", Description = "Stealth, sneak attacks, locks." },
        new() { Name = "fighter", Title = "Fighter", Description = "Steel and endurance." },
        new() { Name = "archer", Title = "Archer", Description = "Death at range." },
        new() { Name = "mage", Title = "Mage", Description = "War and life magic." },
    ];
}
