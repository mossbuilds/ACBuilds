namespace TriviaNight;

public class Question
{
    public string Text { get; set; } = "";
    public List<string> Answers { get; set; } = new();
}

public class Settings
{
    /// <summary>Off by default.</summary>
    public bool Enabled { get; set; } = false;
    public int DefaultRounds { get; set; } = 5;
    public int MaxRounds { get; set; } = 20;
    public int HintAfterSeconds { get; set; } = 20;
    public int RoundTimeoutSeconds { get; set; } = 45;
    public int PauseSeconds { get; set; } = 8;
    /// <summary>Pyreals for the sole top scorer; 0 = no prize.</summary>
    public int WinnerPyreals { get; set; } = 0;
    /// <summary>Hard cap applied to WinnerPyreals.</summary>
    public int MaxPyreals { get; set; } = 5000;

    // Review the wording/facts before enabling.
    public List<Question> Questions { get; set; } = new()
    {
        new() { Text = "What is the name of the world Asheron's Call takes place on?", Answers = { "Dereth" } },
        new() { Text = "What is the currency of Dereth?", Answers = { "Pyreal", "Pyreals" } },
        new() { Text = "Which insectoid alien race invaded Dereth?", Answers = { "Olthoi" } },
        new() { Text = "Which green, club-carrying humanoids fill early dungeons like the Drudge Hideouts?", Answers = { "Drudge", "Drudges" } },
        new() { Text = "Which small frog-like monsters, common near Holtburg, are the classic first kills?", Answers = { "Mosswart", "Mosswarts" } },
        new() { Text = "What is the starting town most Aluvian characters begin near?", Answers = { "Holtburg" } },
        new() { Text = "Which human race hails from the desert lands: Aluvian, Gharu'ndim or Sho?", Answers = { "Gharu'ndim", "Gharundim" } },
        new() { Text = "Which human race is the one whose name is also the Aluvian homeland's people: Aluvian, Gharu'ndim or Sho?", Answers = { "Aluvian", "Aluvians" } },
        new() { Text = "Which Sho settlement is the classic starting town for Sho characters? (starts with 'S')", Answers = { "Shoushi" } },
        new() { Text = "Which Gharu'ndim starting town starts with 'Y'?", Answers = { "Yaraq" } },
        new() { Text = "What is the name of the magic school that heals and manipulates vitals: ___ Magic?", Answers = { "Life", "Life Magic" } },
        new() { Text = "Which mage skill is used to make targets weaker or stronger by enchanting creatures: ___ Enchantment?", Answers = { "Creature", "Creature Enchantment" } },
        new() { Text = "What is the name of the game's three-letter-free favourite pet-like crafting skill for making potions: ___ ?", Answers = { "Alchemy" } },
        new() { Text = "What is the name of the creature type that gives Tusker fame: the tall, tusked ___ ?", Answers = { "Tusker", "Tuskers" } },
        new() { Text = "Which mysterious figure is the creator of the Portal Storm and the hero-summoner of Dereth?", Answers = { "Asheron" } }
    };
}
