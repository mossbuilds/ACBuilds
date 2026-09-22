using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace RareTally;

/// <summary>
/// Player command: /raretally
/// Reports the caller's own lifetime per-tier rare-find counts (RaresTierOne..Six) and, if the server has real-time
/// rares turned on, a rough "time since last rare" derived from RaresLoginTimestamp. Every value here is already
/// tracked and written by ACE itself in Corpse.cs's TryGenerateRare (the real-time-rares pity-timer math) - this
/// mod only reads and displays it. Read-only: never increments, resets, or writes any of these properties.
/// </summary>
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

    // Verified against ACE master, re-fetched for this build:
    // - Source/ACE.Server/WorldObjects/Player_Properties.cs - the declaring file for all seven fields (unlocated as
    //   of the Round 13 idea text, which flagged it explicitly; it is not in Player_Character.cs either, checked
    //   this round). Each tier counter is a PropertyInt-backed public instance property on partial class Player:
    //     public int RaresTierOne { get => GetProperty(PropertyInt.RaresTierOne) ?? 0; set { ... SetProperty(...); } }
    //   identically for RaresTierTwo/Three/Four/Five/Six. A commented-out RaresTierSeven (and RaresTierSevenLogin)
    //   block exists right alongside them and is inert - mirrored here by not adding a fabricated seventh counter.
    //   RaresLoginTimestamp is the matching public int? property (PropertyInt.RaresLoginTimestamp) that anchors the
    //   pity timer.
    // - Source/ACE.Server/WorldObjects/Corpse.cs - TryGenerateRare(DamageHistoryInfo killer) confirms both how these
    //   are written (killerPlayer.RaresTierOne++ / RaresTierOneLogin = timestamp per tier, on a successful rare drop)
    //   and how the pity timer itself reads them: realTimeRares/realTimeRaresAlt come from
    //   PropertyManager.GetBool("rares_real_time").Item / PropertyManager.GetBool("rares_real_time_v2").Item, and
    //   when either is on, killerPlayer.RaresLoginTimestamp is compared against the current Unix timestamp
    //   (via Time.GetDateTimeFromTimestamp) to decide the pity bonus. This command reads the exact same three server
    //   properties and the exact same RaresLoginTimestamp value, never recomputing or resetting it.
    [CommandHandler("raretally", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows your lifetime per-tier rare-find counts.")]
    public static void HandleRareTally(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Rare tally is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        Reply(session,
            $"Rare finds - Tier 1: {player.RaresTierOne}, Tier 2: {player.RaresTierTwo}, Tier 3: {player.RaresTierThree}, " +
            $"Tier 4: {player.RaresTierFour}, Tier 5: {player.RaresTierFive}, Tier 6: {player.RaresTierSix}.");

        var realTimeRares = PropertyManager.GetBool("rares_real_time").Item;
        var realTimeRaresAlt = PropertyManager.GetBool("rares_real_time_v2").Item;

        if ((realTimeRares || realTimeRaresAlt) && player.RaresLoginTimestamp.HasValue)
        {
            var nowUnix = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var elapsedSeconds = nowUnix - player.RaresLoginTimestamp.Value;

            if (elapsedSeconds >= 0)
            {
                var elapsed = TimeSpan.FromSeconds(elapsedSeconds);
                Reply(session, $"Time since your last rare (or pity-timer anchor): {(int)elapsed.TotalDays}d {elapsed.Hours}h {elapsed.Minutes}m.");
            }
            else
            {
                // realTimeRares mode stores a future "next chance" timestamp, not a past one, while it's ticking down
                var remaining = TimeSpan.FromSeconds(-elapsedSeconds);
                Reply(session, $"Pity-timer bonus becomes available in about {(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes}m.");
            }
        }
    }
}
