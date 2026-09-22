using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ChessStats;

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
    // - Source/ACE.Server/Entity/Chess/ChessMatch.cs: ChessMatch.Finish(int) reads/writes rank and record
    //   entirely through `player.GetProperty(PropertyInt.X)` / `player.SetProperty(PropertyInt.X, ...)` on the
    //   base Player/IPlayer object it looked up - it never touches a private field of ChessMatch itself for
    //   these values. The properties used, all PropertyInt (ACE.Entity.Enum.Properties):
    //     PropertyInt.ChessTotalGames  - incremented on every finished game with a real winner (`totalGames = (player.GetProperty(...) ?? 0) + 1`)
    //     PropertyInt.ChessGamesWon    - incremented for the winning side
    //     PropertyInt.ChessGamesLost   - incremented for the losing side
    //     PropertyInt.ChessRank        - read/written by the static ChessMatch.AdjustPlayerRanks(...), default 1400 when unset,
    //                                    delta = Math.Round(RankFactor * (win - chance)), RankFactor = 50 (public const int on ChessMatch)
    //   GetProperty/SetProperty are the same public accessor pattern every other property-backed stat in this repo
    //   already reads (e.g. BurdenCheck's EncumbranceVal, LuminanceLedger's AvailableLuminance). No reflection needed.
    // - Source/ACE.Server/WorldObjects/Player_Chess.cs: `public ChessMatch ChessMatch;` on the partial Player class -
    //   confirmed public field, used here only to report "currently in a match" vs. not (never read or written by this mod).
    // - Source/ACE.Server/Managers/PlayerManager.cs: `public static IPlayer FindByName(string name, out bool isOnline)` -
    //   confirmed public static, works for both online and offline characters (looks up the shared playerNames index).
    //   IPlayer exposes GetProperty (used elsewhere in the same file, e.g. GagPlayer's `player.SetProperty(...)` on an
    //   IPlayer returned from FindByName), so an admin can look up an offline player's chess stats too.
    // No property here is ever set or reset by this mod - every write happens naturally through real chess games.
    [CommandHandler("chessrank", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Shows the built-in chess rank, games played, and win/loss record for you or (admin) another player.", "[player name]")]
    public static void HandleChessRank(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Chess rank lookup is not available."); return; }

        var requester = session?.Player;
        if (requester == null) return;

        var targetName = parameters.Length > 0 ? string.Join(" ", parameters) : null;

        if (targetName == null)
        {
            Report(session, requester, requester.Name, requester.ChessMatch != null);
            return;
        }

        if (session!.AccessLevel < AccessLevel.Admin)
        {
            Reply(session, "Only an admin can check another player's chess rank.");
            return;
        }

        var target = PlayerManager.FindByName(targetName, out var isOnline);
        if (target == null)
        {
            Reply(session, $"No player named '{targetName}' found.");
            return;
        }

        var inMatch = isOnline && (target as Player)?.ChessMatch != null;
        Report(session, target, target.Name, inMatch);
    }

    private static void Report(Session session, IPlayer player, string name, bool inMatch)
    {
        var rank = player.GetProperty(PropertyInt.ChessRank) ?? 1400;
        var total = player.GetProperty(PropertyInt.ChessTotalGames) ?? 0;
        var won = player.GetProperty(PropertyInt.ChessGamesWon) ?? 0;
        var lost = player.GetProperty(PropertyInt.ChessGamesLost) ?? 0;

        Reply(session, $"{name}'s chess rank: {rank} | games: {total} (won {won}, lost {lost}){(inMatch ? " | currently in a match" : "")}");
    }
}
