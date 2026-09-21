# AdminAudit (proposed, not deployed)

Appends a line to a log file (and the server log) for every in-game command at or above `MinLevel` (default Advocate), which covers admin spawns (`/create`, `/ci`, `/createlist` etc.). No commands of its own.

Settings.json: `Enabled`, `LogFile`, `MinLevel` (AccessLevel), `RedactArgs` (commands whose arguments are never written; account/password commands by default). Does not touch ace_auth/ace_shard.

Verified against ACE source: `CommandManager.GetCommandHandler(Session, string, string[], out CommandHandlerInfo)` public static, `CommandHandlerResponse.Ok/SudoOk`, `CommandHandlerInfo.Attribute` (all ACE.Server.Command); `Session.Player`, `Session.AccessLevel` (ACE.Server.Network). Called from GameActionTalk for in-game commands only (console commands are not logged).

Risks: log grows unbounded (rotate externally). Test: run `/create 1` as admin, read the log. Enable later by copying to mods/.
