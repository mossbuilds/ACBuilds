# TriviaNight

Admin-run chat trivia. Off by default (`Enabled=false` in Settings and Meta.json).

- `@trivia start [rounds]`, `@trivia stop`, `@trivia status` (Admin; ACE commands use the `@` prefix in chat). No built-in trivia command was found; re-grep `[CommandHandler("trivia"` at check time.
- Questions live in `Settings.json` (`Questions`: `Text`, `Answers[]`, case-insensitive, punctuation ignored). ~15 AC defaults ship; review the wording and facts before enabling.
- Flow: question via `PlayerManager.BroadcastToAll`, first matching local-chat line wins the round, hint after `HintAfterSeconds`, timeout after `RoundTimeoutSeconds`, `PauseSeconds` between rounds, scoreboard at the end.
- Hook: Harmony postfix on `Player.HandleActionTalk(string)`; the original runs first so the answer is heard normally. Gagged players do not count. Global/general chat is not hooked.
- Prize: `WinnerPyreals` (0 = none, capped by `MaxPyreals`) to the sole top scorer, as `coinstack` items via `TryCreateInInventoryWithNetworking` queued with `ActionChain(player, ...)`.
- No world objects placed; state in memory only; no ace_auth/ace_shard access; no account data in messages.
