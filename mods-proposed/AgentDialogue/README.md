# AgentDialogue (proposed, not deployed, not compiled)

Lets one or more whitelisted NPC weenies (`Settings.json` -> `WatchedWcids`, empty by default) have their in-game
responses driven by an outside process instead of static emotes. Off by default (`Enabled: false`); with no watched
wcids the Harmony patches are installed (Harmony patches by method, not by call site) but do nothing, since every
postfix checks the wcid against an empty watch-list first.

## Hook

Harmony **postfixes** on both:

- `ACE.Server.WorldObjects.Managers.EmoteManager.OnHearChat(Player player, string message)` - NPC heard local chat
  from a nearby player.
- `ACE.Server.WorldObjects.Managers.EmoteManager.OnTalkDirect(Player player, string message)` - a player talked
  directly to the NPC (`/te`, or the "talk to" right-click).

Both are public instance methods on the NPC's own `EmoteManager` (verified against `EmoteManager.cs`, master branch).
The NPC itself is `__instance.WorldObject` (the `EmoteManager.WorldObject` property, which resolves to a proxy object
if one is set, e.g. a Hooker on a Hook - same object a static emote would have run against). This fires whether or
not the weenie has any `HearChat`/`ReceiveTalkDirect` emotes configured, per the plan. No emote set is created, run
or altered - the mod only observes the same call ACE already makes on every hear/talk-direct event.

## File contract (server working dir, created if missing)

- `agent/dialogue/inbox.jsonl` - one JSON object appended per watched-NPC hear/talk-direct event:
  `{"time": <unix seconds>, "npc_wcid": <uint>, "npc_name": "...", "speaker": "<character name>", "text": "...", "landblock": "XXXX"}`.
  Character name only - never an account name or IP. `text` is control-character-stripped and capped at
  `Settings.MaxLength` (default 500) before it is ever written.
- `agent/dialogue/outbox.jsonl` - the outside process appends one JSON object per line: `{"npc_wcid": <uint>, "text": "..."}`.
  Polled every `Settings.PollSeconds` (clamped to >=1s). Untrusted input: any line that fails to parse, has no
  `npc_wcid`/`text`, or targets a wcid not on `WatchedWcids` is logged and dropped, never spoken. `text` is
  control-character-stripped and capped at `Settings.MaxLength` the same as the inbox side.

Both paths are relative to the server working dir; directories are created with `Directory.CreateDirectory` under
the same lock used for the write. All file access is wrapped in try/catch and serialized through a single lock
object, per the BugNote/TradeLedger pattern - a bad line or a locked file never throws into the game tick.

## Speaking

A queued outbox line is spoken as a normal **Say** broadcast to nearby players - the same
`GameMessageHearSpeech(text, name, npc.Guid.Full, ChatMessageType.Emote)` call ACE's own `EmoteType.Say` case uses
(`EmoteManager.ExecuteEmoteSet`), sent via `npc.EnqueueBroadcast(..., npc.LocalBroadcastRange)`. Chosen over `Tell`
so the reply is visible to everyone nearby, the way a real NPC conversation would be, rather than whispered to one
player. This is documented here as the explicit choice the plan asked for.

Only a *currently-loaded* instance of the watched wcid is spoken from (`LandblockManager.GetLoadedLandblocks()` +
`GetAllWorldObjectsForDiagnostics()`, same enumeration `HotspotAlert` uses). A line targeting an NPC that isn't
loaded yet is retried on the next few polls (up to `Settings.MaxRetryPolls`, default 30) and then dropped with a log
line, so a stale line can't sit in the file forever. All world-object work (the broadcast itself) is queued through
`new ActionChain(WorldManager.ActionQueue, ...).EnqueueChain()`, never called directly from the timer thread.

After each poll, `outbox.jsonl` is rewritten with only the lines still pending (unmatched, still-loading NPC) -
consumed lines (spoken, or refused as invalid/off-watch-list) are dropped. This happens under the same file lock.

No LLM call, no network call, and no `ace_auth`/`ace_shard` access happens inside this mod - reading/writing the
outside process's replies is entirely the outside process's job, per the plan's shared contract.

## Settings

`Enabled` (false), `WatchedWcids` (empty list), `InboxFile` (`agent/dialogue/inbox.jsonl`), `OutboxFile`
(`agent/dialogue/outbox.jsonl`), `PollSeconds` (2, clamped >=1), `MaxLength` (500), `MaxRetryPolls` (30).

No admin command is included - there is nothing here an admin needs to do at the console beyond editing
`Settings.json` (the watch-list, the poll interval), and no ACE built-in name collision to worry about as a result.

## Unverified

- Compilation (not built, per instructions).
- `EmoteManager.WorldObject` always resolving to the intended NPC rather than a proxy object in every configuration
  a watched wcid could be placed in (Hooker-on-Hook and similar proxy cases are handled by that property itself, per
  its own doc comment, but not exercised here).
- Exact wording/format of the in-game "talk to" trigger that calls `OnTalkDirect` versus ordinary local chat that
  calls `OnHearChat` - both are hooked identically, so this only matters for anyone reading `inbox.jsonl` who wants
  to tell the two apart (nothing here distinguishes them - both write the same record shape; add a `via` field if
  that distinction turns out to matter).
- Whether `GetAllWorldObjectsForDiagnostics()` is cheap enough to call every `PollSeconds` across every loaded
  landblock on a busy server - `HotspotAlert` does the same enumeration on its own timer, but that mod's default
  interval is 30s+ where this one defaults to 2s; raise `PollSeconds` if that shows up as tick load in practice.
