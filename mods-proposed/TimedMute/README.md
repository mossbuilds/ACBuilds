# TimedMute

Admin-only mute with a custom duration, a reason, and a persisted list.

**Already in ACE:** `/gag name` (fixed 5 minutes, Sentinel) and `/ungag`. The stock gag already expires (`Player.GagsTick`) and persists with the character. ACE has no `/mute`, `/unmute` or `/mutes`. **Added here (genuinely missing):** custom minutes, reason and issuer record, a list, and re-applying the remaining time at login.

- `/mute <name> <minutes> [reason]` (Sentinel, needs world). Underscores stand for spaces in names. Online targets only; refuses staff (Advocate+). Writes the stock gag properties with the chosen duration via `SetProperty` + `SaveBiotaToDatabase` (ACE's own path; no direct ace_auth/ace_shard access).
- `/unmute <name>`, `/mutes`.
- State: `timedmute-list.json` (setting `DataFile`). A 15 s timer lifts expired mutes, queued via `ActionChain(player, ...)`. A postfix on `Player.PlayerEnterWorld` re-applies the remaining time, or lifts it if expired.
- Settings: `DefaultMinutes` (10), `MaxMinutes` (1440), `DataFile`.
- Risks: the stock countdown only ticks online, so the JSON clock is authoritative and corrected at login. Test on a throwaway character.
- Enable later: copy to mods/ after `check-mod.sh TimedMute` prints `MOD OK`.
