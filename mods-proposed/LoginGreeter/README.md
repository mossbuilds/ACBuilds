# LoginGreeter

ACE already sends a single server-wide `server_motd` (WorldManager.cs, same text for everyone, every login) plus popup_welcome/popup_motd. This mod adds only what is missing:

- a different message for a character's first login (`Character.TotalLogins <= 1`) vs returning logins,
- a "players online" line,
- optional first-login tips,
- a `/rules` command (no ACE built-in of that name; grepped the CommandHandler names).

It does not touch `server_motd`. It reads/writes no databases and sends no account names, IPs or passwords; only the character name and the online count.

## Settings.json
`Enabled` (default true), `DelaySeconds` (3), `FirstLoginMessage`, `ReturningMessage`, `OnlineMessage` (empty disables; `{0}` = count), `FirstLoginTips`, `Rules`. `{0}` = character name in every text except OnlineMessage. Braces in texts must be written `{{` `}}`.

## Risks
Chat sent before the client is ready is lost, hence the delay. Harmless otherwise.

## Test
Log in a new character (first message + tips), relog (returning), run `/rules`.

## Enable later
Copy the built folder to the server mods directory (Tom's decision).
