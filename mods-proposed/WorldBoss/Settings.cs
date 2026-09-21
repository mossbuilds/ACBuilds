namespace WorldBoss;

public class Settings
{
    public bool Enabled { get; set; } = false;
    public uint BossWcid { get; set; } = 900021232; // Old Whiskers
    /// <summary>true = spawn next to the admin who ran /worldboss start; false = at the configured position (always used for scheduled starts).</summary>
    public bool SpawnNextToAdmin { get; set; } = false;
    public string Cell { get; set; } = "0x00000000"; // hex cell, e.g. 0xA9B40019
    public float X { get; set; } = 0f;
    public float Y { get; set; } = 0f;
    public float Z { get; set; } = 0f;
    public int MaxMinutes { get; set; } = 30;
    /// <summary>Pyreal for the top damage dealer; 0 = none.</summary>
    public int RewardPyreals { get; set; } = 0;
    public bool ScheduleEnabled { get; set; } = false;
    /// <summary>Comma list, e.g. "Sat,Sun".</summary>
    public string Days { get; set; } = "Sat";
    /// <summary>Server-local time HH:mm.</summary>
    public string Time { get; set; } = "20:00";
    public string StartMessage { get; set; } = "{0} has appeared! Defeat it!";
    public string DeathMessage { get; set; } = "{0} has been slain! Most damage: {1}.";
    public string DespawnMessage { get; set; } = "{0} has vanished.";
}
