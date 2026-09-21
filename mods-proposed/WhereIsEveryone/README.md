# WhereIsEveryone (proposed, not deployed)
Admin player finder. Verified in ACE source: `PlayerManager.GetAllOnline()`, `GetOnlinePlayer(string)`, `Player.Teleport(Position, bool)`, `Position.Landblock/ToLOCString()`.
Commands (all Admin): `/who2` lists online players with level and location; `/gotoplayer <name>`; `/bringplayer <name>`.
Settings.json: none used. Risks: minimal; Teleport is not thread-safe off the world thread (command handlers run on it). Name `who2` avoids clashing with ACE built-ins.
Test: log two characters in, run each command. Enable later: copy folder to the mods dir.
