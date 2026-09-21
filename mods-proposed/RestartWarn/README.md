# RestartWarn (proposed, not deployed)
Adds extra countdown warnings for shutdowns ACE itself schedules (ACE's own `/shutdown` flow) and a player `/restartwhen` query. It only READS `ServerManager.ShutdownInitiated` / `ShutdownTime` (verified in ACE.Server.Managers.ServerManager) and broadcasts via `PlayerManager.BroadcastToAll`. It never shuts down or restarts anything, and runs no docker or shell commands.
Commands: `/restartwhen` (Player). Name checked against ACE built-ins (no clash).
Settings.json: `WarnAtSeconds` (180, 45, 20), `Suffix`. Read at world open.
Risks: ShutdownTime is only set once ACE's shutdown thread starts, so warnings begin a second or so after the command. Timer thread only enqueues sends.
Test: run ACE's `/shutdown 60` on a test server and watch for 45 s and 20 s notices; `/shutdown-cancel` style cancel clears state. Enable later: copy folder to the mods dir.
