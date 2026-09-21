# BugNote

Players leave a short note about a bug or stuck spot; online admins see it at once and can list it later.

**Already in ACE:** `/reportbug` only builds a URL for the player to open in a browser; it logs nothing on the server. BugNote is a separate, in-game log (no command name clashes: `bugnote`, `bugnotes`, `bugnoteclear` are not built in).

- `/bugnote <text>` (Player): saves one note, replies with the player's own note count. Rate-limited per player (`CooldownSeconds`).
- `/bugnotes [n]` (Sentinel): last n notes (max 50). `/bugnoteclear` (Sentinel): clears all.
- Each note stores only: character name, time, landblock (hex), text (control characters stripped, `MaxLength`). Never account names, IPs, sessions or passwords.
- State: `bugnotes.json` (setting `DataFile`, in the mod folder); oldest dropped beyond `MaxNotes`. No ace_auth/ace_shard access.
- Admin whispers are queued with `ActionChain(admin, ...)`.
- Settings: `CooldownSeconds` (60), `MaxLength` (200), `MaxNotes` (500), `DataFile`.
- Not compiled yet: run `check-mod.sh BugNote`.
