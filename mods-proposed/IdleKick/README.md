# IdleKick

Warns, then logs off players idle beyond a long threshold. **Disabled by default** (`Enabled=false`).

- Activity = movement input (postfix `Player.OnMoveToState`). Staff (Advocate+), players not in NonCombat mode and players with an active PK timer are never kicked.
- Warning chat message, then after `GraceSeconds` a normal `Session.LogOffPlayer()` (ACE's own logoff path). No DB access, no process kills.
- Settings: `Enabled`, `IdleMinutes` (60), `GraceSeconds` (120), `WarnMessage` ({0} = grace seconds).
- No commands.
- Risks: chat-only or crafting/idle-but-present players look idle; keep the threshold long. Any movement input resets the timer.
- Test: enable on a test server, set IdleMinutes=1, GraceSeconds=15, stand still on a non-staff character.
- Enable later: set `Enabled` true in Settings.json.
