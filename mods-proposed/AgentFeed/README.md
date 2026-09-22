# AgentFeed (proposed, not deployed, not compiled)

Read-only telemetry so an outside AI agent can watch what's happening on the server. No control, no world objects, no `ace_auth`/`ace_shard` access.

Logs one compact line per event to `agent/feed/events.log` (server working dir, matches the shared `agent/` file contract in `docs/AI_AGENT_HOOKS_PLAN.md`): UTC time, event type, character NAME only (never account names or IPs). Rotates at `MaxKb`, keeps `MaxFiles` (`.1`..`.N`), same pattern as TradeLedger.

Events actually logged:
- **login** - postfix on `Player.PlayerEnterWorld()` (public, verified in `Player_Networking.cs`; same method LoginGreeter already patches).
- **logout** - postfix on `Player.LogOut_Inner(bool)` (verified in `Player_Networking.cs`; same signature MinionCleanup patches).
- **death** - postfix on protected `Player.Die(DamageHistoryInfo, DamageHistoryInfo)`, patched by name with explicit `ArgumentType`s since Harmony attributes can't use `MakeByRefType()` and the method isn't public (same signature MinionCleanup already patches). Includes the killer's name if resolvable (`DamageHistoryInfo.Guid` -> `Player.CurrentLandblock.GetObject(guid)`, both verified against ACE source), never an account/IP.
- **chat heard by a watched NPC** - **not implemented**. `AgentDialogue` does not exist yet in `mods-proposed/` at the time of writing, so there is nothing to reuse; this is a follow-up once AgentDialogue ships (do not block AgentFeed on it, per the plan doc). AgentFeed does not reference AgentDialogue's code.

## Outbound webhook (the only network call in this mod suite)

If `Settings.WebhookUrl` is non-empty, each logged event is also POSTed as a small JSON body (`{time, event, character, ...}`) to that URL via a single reused `HttpClient`. The call runs on `Task.Run`, is never awaited by the patch, has its own timeout (`WebhookTimeoutSeconds`), and every exception is caught and logged - it can never throw back into ACE or block the game thread. Off by default (`WebhookUrl` empty). **This is the only outbound network call anywhere in this mod suite** (AgentDialogue/AgentActions/AgentBridge do not make one); enabling it means this server calls out to whatever URL is configured, so treat that URL like a secret and review it before setting it.

Command (Sentinel+): `/agentfeed` - prints the log path and current settings only (enabled, log file, rotation size, which events are logged, whether the webhook is on and its URL). It is read-only and never toggles anything; all settings live in `Settings.json`. Checked against `%TEMP%\cmds.txt`: `agentfeed` is not a built-in.

Settings: `Enabled` (false), `LogFile` (`agent/feed/events.log`), `MaxKb` (512), `MaxFiles` (5), `LogLogin`/`LogLogout`/`LogDeath` (true), `WebhookUrl` (empty = off), `WebhookTimeoutSeconds` (5).

All file writes are wrapped in try/catch/lock, same as TradeLedger/AdminAudit. Every patch catches and logs its own exceptions and never throws.

Unverified: compile; the exact moment `Player.Die`'s `DamageHistoryInfo` parameters are populated for a PK/monster kill (best-effort killer-name resolution, falls back to `-` if the guid can't be resolved to a live world object); whether `CurrentLandblock` is still set at the point `Die`'s postfix runs (wrapped in try/catch, degrades to `-` rather than throwing).
