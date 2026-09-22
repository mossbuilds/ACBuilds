# AI_AGENT_HOOKS_PLAN.md - a small suite of mods letting an outside AI agent see and touch the game

Plan for a set of 4 independent mods, per Tom's answer (all four): drive an NPC's dialogue, a read-only telemetry feed,
a whitelisted action inbox, and a real local HTTP API. All four ship OFF by default and are built to the same rigor
as everything else in `mods-proposed/`: verified ACE APIs, no `ace_auth`/`ace_shard` access, no world-object spam.

## Shared contract (files, not shared code - mods stay independent)
A folder under the server working dir, `agent/`, is the common language between them:
- `agent/dialogue/inbox.jsonl` - AgentDialogue appends one line per thing a whitelisted NPC heard (speaker, text, time).
- `agent/dialogue/outbox.jsonl` - an outside process appends a reply line; AgentDialogue polls and speaks it, then truncates what it consumed.
- `agent/actions/inbox.jsonl` - AgentActions polls for whitelisted action requests (one JSON object per line).
- `agent/actions/log.jsonl` - every action AgentActions actually executed (or refused), for audit.
- `agent/feed/events.log` - AgentFeed's rotating read-only event log.
AgentBridge (the HTTP mod) reads/writes these same files, so it's an optional convenience layer, not a requirement -
an outside process can drive everything by just watching/writing files, with zero network exposure, if that's all Tom wants running.

## The four mods
1. **AgentDialogue** - lets one or more whitelisted NPC weenies (by wcid) have their `Use`/`HearChat`/`ReceiveTalkDirect` emote
   replaced by an outside brain: when a player talks to/near the NPC, it's appended to `inbox.jsonl`; a periodic poll checks
   `outbox.jsonl` for a reply keyed to that NPC and speaks it verbatim via a normal `Tell`/`Say`. No LLM call happens inside
   the game process - that's the outside process's job (keeps API keys and unpredictable latency out of the game tick).
2. **AgentFeed** - read-only. Logs a compact line per notable event (player login/logout, death, chat heard by a
   watched NPC, quest completion if cheaply available) to a rotating file, exactly like TradeLedger's rotation.
   Optional: an outbound webhook POST (HttpClient, fire-and-forget, never blocks the game thread) to a configured URL -
   this is the only place any mod here makes an outbound network call, and it's off unless a URL is set.
3. **AgentActions** - polls `agent/actions/inbox.jsonl` for whitelisted requests only: `speak` (a watched NPC says a line),
   `broadcast` (server-wide announcement), `spawn` (wcid must be on an explicit allowlist - reuses the RaiseSkeleton/WorldBoss
   pattern of cloned, harmless weenies, never an arbitrary wcid), `teleport_npc` (move an already-placed NPC weenie, never a
   player). Every request is validated, executed via `ActionChain` like every other mod here, and logged to `log.jsonl`
   whether accepted or refused. No player teleports, no item grants, no account/character data - this is a content-puppeting
   tool, not an admin-power tool.
4. **AgentBridge** - a local-only HTTP listener (`HttpListener`, bind `127.0.0.1` only, never `0.0.0.0` or a public
   interface) exposing thin read/write wrappers over the same files: `GET /state` (online players, positions, the watched
   NPCs' last-heard lines), `POST /dialogue/{npc}/reply`, `POST /actions` (same schema AgentActions reads from disk).
   Requires a shared-secret header set in `Settings.json` (comparison must be constant-time or at least not a source of a
   timing side-channel worth caring about for a private server - simple string compare is acceptable here, documented as such).
   This is the mod that most needs a careful read before enabling: it is the only one that opens a socket.

## Guardrails (apply to all four)
- Ship `Enabled: false`. AgentBridge additionally ships with no default `SharedSecret` (a mod that starts with a blank
  secret must refuse to bind rather than accept unauthenticated requests) and its own `README.md` warning read this first.
- Every file write goes through the same wrapped-try/catch/lock pattern already used by BugNote/TradeLedger/MentorRank.
- AgentActions' allowlists (spawnable wcids, teleportable NPC wcids) are empty by default - an admin has to opt each one in.
- Verify every ACE API and namespace against source, same as every other mod in this repo; command names checked against
  the 327 built-ins in `%TEMP%\cmds.txt`.
- None of these mods are deployed or pushed by default - source only, same as everything else here, until Tom says otherwise.

## Build order
AgentDialogue and AgentFeed first (lowest risk, no network, prove the file contract). AgentActions next (still no
network, but it *does* things). AgentBridge last, and reviewed the most carefully, since it's the one opening a port.
