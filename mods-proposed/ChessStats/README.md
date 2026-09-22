# ChessStats

Read-only `/chessrank [player]`: shows the built-in chess minigame's own Elo-style rank, total games, and win/loss record, which ACE already computes internally but only ever shows once in the in-match win/loss popup. With no argument, shows your own stats (any player). With a name, shows that player's stats (admin only) - works whether the target is online or offline. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, no writes - purely reads properties chess already maintains.

## Idea (IDEAS.md, Round 10, #62)

> ChessStats (feas 5) - Read-only player command surfacing the built-in Chess minigame's own Elo-style rank and win/loss tracking, which ACE already computes but never displays outside the in-game chess UI's one-time win/loss popup - there is no way to check standing between games. [...] Cmd: `/chessrank [player]` (Player; own stats only, or Admin for any name via PlayerManager.FindByName, already-verified pattern).

## Verified against ACE master, re-fetched for this build

- `Source/ACE.Server/Entity/Chess/ChessMatch.cs` - `ChessMatch.Finish(int winner)` reads and writes chess stats entirely through the base Player/IPlayer object's own public `GetProperty` / `SetProperty`, never through a private field of `ChessMatch` itself:
  - `PropertyInt.ChessTotalGames` - incremented every finished game with a real winner: `var totalGames = (player.GetProperty(PropertyInt.ChessTotalGames) ?? 0) + 1; player.SetProperty(PropertyInt.ChessTotalGames, totalGames);`
  - `PropertyInt.ChessGamesWon` / `PropertyInt.ChessGamesLost` - incremented for the winning/losing side the same way.
  - `PropertyInt.ChessRank` - read/written by the static `ChessMatch.AdjustPlayerRanks(playerGuid, opponentGuid, winnerGuid)`: `var rank = player.GetProperty(PropertyInt.ChessRank) ?? 1400;` ... `var delta = (int)Math.Round(RankFactor * (win - chance)); player.SetProperty(PropertyInt.ChessRank, rank + delta);` - `RankFactor` is a `public const int` on `ChessMatch` equal to `50`. Default rank when unset is 1400, matching what a never-played player would see.
  - All four are `PropertyInt` (`ACE.Entity.Enum.Properties`), read via `GetProperty`/`SetProperty` - the exact pattern every other property-backed stat mod in this repo already uses (BurdenCheck's `EncumbranceVal`, LuminanceLedger's `AvailableLuminance`). **Public, no reflection needed.**
- `Source/ACE.Server/WorldObjects/Player_Chess.cs` - `public ChessMatch ChessMatch;` on the partial `Player` class. Confirmed public field. This mod only reads it to report "currently in a match" vs. not; never writes it.
- `Source/ACE.Server/Managers/PlayerManager.cs` - `public static IPlayer FindByName(string name, out bool isOnline)`. Confirmed public static, looks up the shared `playerNames` index so it resolves both online and offline characters by name. `IPlayer` exposes `GetProperty`/`SetProperty` (used the same way elsewhere in `PlayerManager.cs`, e.g. `GagPlayer`'s `player.SetProperty(...)` on an `IPlayer` returned from `FindByName`), so an admin lookup of an offline player's chess stats works without a null Player cast.

No member needed a reflection fallback - everything the idea named is public exactly as claimed.

## Command

- `/chessrank` - player, no args: your own rank, games, won/lost, and whether you're currently in a match.
- `/chessrank <name>` - admin only: the same for another player, found via `PlayerManager.FindByName` (online or offline). A non-admin caller gets "Only an admin can check another player's chess rank."; base command is `AccessLevel.Player` so anyone can check themselves.

## Risks

None identified. A player who has never played chess simply shows the 1400 default and 0 games, matching what `Finish`/`AdjustPlayerRanks` would compute on their first game. Nothing here writes any property - purely a read-only report.

## Command name check

`chessrank` is not a built-in per `%TEMP%\cmds.txt` (only `debugchess` appears there, unrelated).

## Test

Set `Enabled=true` in Settings.json and Meta.json, run `/chessrank`, then (as admin) `/chessrank <name>` for another character.
