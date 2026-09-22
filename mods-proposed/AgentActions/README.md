# AgentActions (proposed, not deployed, not compiled)

Polls `agent/actions/inbox.jsonl` for whitelisted action requests from an outside process, validates each one
strictly, executes only what is allowed, and appends the outcome to `agent/actions/log.jsonl` - accepted+result, or
refused+reason. Off by default (`Enabled: false`). No Harmony patches - this mod only *calls* ACE, the same shape as
`EventClock`; the `[HarmonyPatch]` class attribute is present only because `BasicPatch<T>` requires it.

## File contract (server working dir, created if missing)

- `agent/actions/inbox.jsonl` - the outside process appends one JSON object per line, one of the four shapes below.
  Every line present at the start of a poll is consumed that poll: valid, allowed requests are queued on an
  `ActionChain` (so the outcome may still fail asynchronously, e.g. a blocked spawn position - that failure is logged
  too); refused or malformed lines are logged immediately. Either way the file is rewritten empty under the same lock
  used for the read, matching AgentDialogue's outbox-consumption pattern (read all lines under lock, decide, rewrite
  under lock).
- `agent/actions/log.jsonl` - append-only audit trail, one JSON object per request: `{"time", "status": "accepted"|
  "refused", "type", "request": <original line>, "result"|"reason": ...}`. Never truncated or rewritten by this mod.

Both paths are relative to the server working dir (`Settings.InboxFile`/`Settings.LogFile`). Polled every
`Settings.PollSeconds` (clamped to >=1s, default 2). All file access is wrapped in try/catch and serialized through a
single lock object, per the BugNote/TradeLedger pattern - a bad line or a locked file never throws into the game
tick, and a `Timer` callback exception is caught and logged, never rethrown into the timer thread.

## The four action types (exactly these four - no others)

1. **`speak`** - `{"type":"speak","npc_wcid":<uint>,"text":"..."}`. Requires an already-loaded instance of `npc_wcid`
   (`LandblockManager.GetLoadedLandblocks()` + `GetAllWorldObjectsForDiagnostics()`, same enumeration AgentDialogue
   uses); refused if none is loaded. `text` is control-character-stripped and capped at `Settings.MaxTextLength`
   (default 500); refused if empty after cleaning. Spoken via the same Say-broadcast AgentDialogue's outbox uses:
   `npc.EnqueueBroadcast(new GameMessageHearSpeech(text, name, npc.Guid.Full, ChatMessageType.Emote),
   WorldObject.LocalBroadcastRange)`, queued on `new ActionChain(WorldManager.ActionQueue, ...)`.

2. **`broadcast`** - `{"type":"broadcast","text":"..."}`. Same text cap/stripping as `speak`. Rate-limited to
   `Settings.MaxBroadcastsPerHour` (default 6, rolling 1-hour window tracked in memory, lost on restart) since this
   reaches every player - refused once the window is full, before anything is sent. Sent via
   `PlayerManager.BroadcastToAll(new GameMessageSystemChat(text, ChatMessageType.WorldBroadcast))`, the exact call
   verified in `PlayerManager.cs` and already used by `AnnounceEvents`/`WorldBoss`/`EventClock` etc. in this repo.

3. **`spawn`** - `{"type":"spawn","wcid":<uint>,"near_npc_wcid":<uint>}`. Refused unless `wcid` is in
   `Settings.AllowedSpawnWcids` (empty by default - nothing is spawnable until an admin opts wcids in). Looks the
   wcid up with `DatabaseManager.World.GetCachedWeenie(wcid)` (refused if not found) and checks its `Type` field
   (the raw int backing `WeenieType`) against `Admin`/`Sentinel`/`Undef` - refused regardless of the allowlist. This
   is defense in depth: the allowlist should already keep those wcids out, but a misconfigured `Settings.json` entry
   is still refused. Requires an already-loaded instance of `near_npc_wcid` (refused if none is loaded). Spawns with
   `WorldObjectFactory.CreateNewWorldObject(weenie)`, positions it with `nearNpc.Location.InFrontOf(2.5f)` +
   `new LandblockId(obj.Location.GetCell())`, then `EnterWorld()` - the exact CreateNewWorldObject/InFrontOf/EnterWorld
   sequence `RaiseSkeleton.SummonFeral` and `WorldBoss.Start` use. The spawned object's own `WeenieType` is checked
   again (belt-and-braces) and destroyed without entering the world if it somehow matches the forbidden set; a
   failed `EnterWorld()` (blocked position) destroys the object and logs a refusal rather than leaving an orphaned
   biota. All of this runs inside `new ActionChain(WorldManager.ActionQueue, ...)`, per repo convention.

4. **`teleport_npc`** - `{"type":"teleport_npc","npc_wcid":<uint>,"cell":<uint|"0xHEX">,"x":<float>,"y":<float>,
   "z":<float>}`. Requires an already-loaded instance of `npc_wcid`; **hard-refused if that instance `is Player`**,
   checked before anything else runs, regardless of what wcid was asked for. `cell` accepts either a JSON number or a
   `"0xHEX"` string (parsed the same way `WorldBoss.ConfiguredPosition` parses its configured cell). `x`/`y`/`z` are
   sanity-checked as finite, non-NaN/non-infinite floats under a loose magnitude bound (~1,000,000 units) - **this
   cannot verify walkability, indoor/outdoor validity, or that the cell actually exists**; a coordinate that passes
   this check can still be a wall, a void cell, or outside the loaded landblock. That is a documented limitation, not
   something this mod can close from here. Moves the object with:
   ```
   npc.Location = new Position(cell, x, y, z, 0, 0, 0, 1);
   LandblockManager.RelocateObjectForPhysics(npc, false);
   ```
   `RelocateObjectForPhysics` is verified in `LandblockManager.cs` (`Source/ACE.Server/Managers/LandblockManager.cs`)
   - its own doc comment says "Relocates an object to the appropriate landblock -- Should only be called from
   physics/worldmanager -- not player!", which is exactly this mod's situation (a non-player NPC, moved by server
   logic, never a player session). It removes the object from its old landblock and adds it to the new one; unlike
   `EnterWorld()` it does **not** replay generator/on-generation hooks, which is correct for "the same NPC, moved."
   `Player.Teleport(Position, bool)` (`Player_Location.cs`) was **not** used here even for reference beyond its
   signature - it does player-session-specific work (fog color, physics-state hides, `GameMessagePlayerTeleport`,
   `OnTeleportComplete`) that does not apply to, and should not be run against, a non-player `WorldObject`.

## Guardrails

- `Settings.Enabled` is `false` by default; with it false, no timer is started and the poll never runs.
- `Settings.AllowedSpawnWcids` is empty by default - `spawn` refuses everything until an admin opts wcids in.
- `speak`/`broadcast` text is always control-character-stripped and length-capped before use or before being logged
  back out, so `log.jsonl` cannot itself become an oversized-text vector.
- `broadcast` is separately rate-limited because, unlike the other three, it reaches every online player.
- `teleport_npc` refuses a `Player`-derived target unconditionally - there is no allowlist or override for this.
- No `ace_auth`/`ace_shard` access anywhere in this mod - only `ACE.Database`'s `GetCachedWeenie` (world content
  lookup, same as `WorldBoss`), and the world/player managers every other mod here already uses.
- No admin command is included. `Settings.json` (the allowlist, the caps, the poll interval) plus reading
  `log.jsonl` is the entire admin-facing surface, so there is nothing to check against `%TEMP%\cmds.txt` for a name
  collision.

## Settings

`Enabled` (false), `AllowedSpawnWcids` (empty list), `MaxTextLength` (500), `MaxBroadcastsPerHour` (6), `PollSeconds`
(2, clamped >=1), `InboxFile` (`agent/actions/inbox.jsonl`), `LogFile` (`agent/actions/log.jsonl`).

## Unverified

- Compilation (not built, per instructions).
- `LandblockManager.RelocateObjectForPhysics`'s behavior on an NPC that has active AI state (a `Monster`'s awareness/
  navigation, an EmoteManager mid-emote, a generator-owned object) - its doc comment scopes it to "physics/
  worldmanager" callers and this mod is exactly that, but it has not been exercised against a live, awake monster
  here; a monster mid-combat or mid-pathing may behave oddly after an instantaneous reposition.
- Whether a `teleport_npc` destination that passes the loose finite/magnitude check but lands inside geometry or
  outside any loaded landblock causes a silent stuck object versus a `LandblockManager` exception - the exception
  path is caught and logged as a refusal-after-the-fact, but the object's location may already have been mutated
  before that exception if `RelocateObjectForPhysics` itself throws partway through (its internal remove/add order
  was read from source but not traced further into `Landblock.RemoveWorldObjectForPhysics`/`AddWorldObjectForPhysics`
  internals).
- Whether `GetAllWorldObjectsForDiagnostics()` at `PollSeconds` (default 2s) across every loaded landblock is cheap
  enough on a busy server - same caveat AgentDialogue's README already raises for its own 2s default.
- Exact behavior when the same `npc_wcid` appears loaded more than once (e.g. two placements of the same wcid on
  different landblocks) - `FindLoadedNpc` returns the first match encountered, which is not deterministic across
  landblock load order.
