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

    // Nine plain questions; review and extend before enabling. Answers are matched case-insensitively.
    public List<Question> Questions { get; set; } = new()
    {
        new() { Text = "What is the name of the world Asheron's Call takes place on?", Answers = { "Dereth" } },
        new() { Text = "What is the currency of Dereth?", Answers = { "Pyreal", "Pyreals" } },
        new() { Text = "Which insectoid race invaded Dereth?", Answers = { "Olthoi" } },
        new() { Text = "Which town is the classic starting town for Aluvian characters?", Answers = { "Holtburg" } },
        new() { Text = "Which town is the classic starting town for Sho characters?", Answers = { "Shoushi" } },
        new() { Text = "Which town is the classic starting town for Gharu'ndim characters?", Answers = { "Yaraq" } },
        new() { Text = "Which of the three heritages (Aluvian, Gharu'ndim or Sho) starts in Yaraq?", Answers = { "Gharu'ndim", "Gharundim" } },
        new() { Text = "Which magic school is used for healing: ___ Magic?", Answers = { "Life", "Life Magic" } },
        new() { Text = "Which crafting skill is used to make potions?", Answers = { "Alchemy" } }
    };
}
