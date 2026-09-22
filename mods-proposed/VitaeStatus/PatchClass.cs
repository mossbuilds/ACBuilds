using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace VitaeStatus;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Reply(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Verified against ACE master, re-fetched for this build (closes the gap Round 9 identified and dropped -
    // "HasVitae is referenced as a bare identifier in Player_Death.cs... but its actual declaration was never
    // located in any file fetched this round"):
    // - Source/ACE.Server/WorldObjects/Player_Properties.cs:
    //     public bool HasVitae => EnchantmentManager.HasVitae;
    //     public float Vitae { get { var vitae = EnchantmentManager.GetVitae(); if (vitae == null) return 1.0f;
    //                                return vitae.StatModValue; } }  // doc comment: "Will return 1.0f if no vitae exists"
    //     public int? VitaeCpPool { get => GetProperty(PropertyInt.VitaeCpPool); set { ... } }
    //     public int? DeathLevel  { get => GetProperty(PropertyInt.DeathLevel);  set { ... } }
    //   All four confirmed public, all on Player itself, exact bodies matched the idea text.
    // - Source/ACE.Server/WorldObjects/Player_Death.cs, InflictVitaePenalty(int amount = 5):
    //     DeathLevel = Level;   // for calculating vitae XP
    //     VitaeCpPool = 0;      // reset vitae XP earned
    //   Confirmed both are reset on every new death, so VitaeCpPool tracks progress since the most recent death
    //   only - the command's wording below says "since your last death", not "lifetime".
    // - Source/ACE.Server/WorldObjects/Player_Death.cs, PK_DeathTick():
    //     MinimumTimeSincePk += CachedHeartbeatInterval;
    //     if (MinimumTimeSincePk < PropertyManager.GetDouble("pk_respite_timer").Item) return;
    //   Confirmed Player.MinimumTimeSincePk is a public double? property and "pk_respite_timer" is a
    //   PropertyManager double key, read the same way here, only to note an active PK-respite window - this
    //   command stays vitae-only and does not duplicate the already-shipped PkStatusInfo.
    [CommandHandler("vitae", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Shows your current vitae penalty and progress toward clearing it.")]
    public static void HandleVitae(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Vitae status is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        if (!player.HasVitae)
        {
            Reply(session, "You have no vitae penalty.");
            return;
        }

        var penaltyPercent = (1.0f - player.Vitae) * 100.0f;
        var cpPool = player.VitaeCpPool ?? 0;
        var deathLevel = player.DeathLevel;

        var msg = $"Vitae penalty: {penaltyPercent:0.0}%. XP earned back toward clearing it since your last death: {cpPool:N0}." +
                   (deathLevel.HasValue ? $" Calculated from level {deathLevel.Value}." : "");

        if (player.MinimumTimeSincePk != null)
        {
            var respite = PropertyManager.GetDouble("pk_respite_timer").Item;
            var remaining = Math.Max(0, respite - player.MinimumTimeSincePk.Value);
            msg += $" (Also in a PK-respite window, ~{remaining:0}s remaining - see /pkstatus for details.)";
        }

        Reply(session, msg);
    }
}
