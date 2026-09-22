# AgentBridge (proposed, not deployed, not compiled)

## READ THIS BEFORE ENABLING

**This is the only mod in the whole `mods-proposed/` tree that opens a network listener.** Every other mod here
only reads/writes plain files or calls into ACE directly. AgentBridge starts a real `System.Net.HttpListener`
bound to loopback (`127.0.0.1`/`localhost`) only - but "loopback only" does **not** mean "only Tom can reach it":
**any other process already running on the same machine that can reach `localhost` can call this API once it's
enabled**, using nothing more than the shared secret in `Settings.json`. That includes any other software on the
box, not just the intended outside agent process.

Recommendations:

- **Leave `Enabled: false` unless Tom has a specific outside process that actually needs HTTP** (as opposed to just
  watching/writing the same `agent/**/*.jsonl` files directly, with zero network exposure - see "What this actually
  is" below).
- **If you do enable it, set a long random `SharedSecret`** (e.g. 32+ random bytes, base64 or hex). The mod refuses
  to start at all if `SharedSecret` is empty or whitespace - it will never run unauthenticated - but a short or
  guessable secret is still weak once something is listening.
- The secret is compared with plain C# string equality (`provided != cfg.SharedSecret`), which is **not a
  timing-safe comparison**. This is a deliberate, documented choice for a private-server, loopback-only convenience
  tool - not something meant to face the internet or a hostile local user. Do not reuse this secret anywhere that
  timing side-channels matter.

## What this actually is - a THIN layer, not a second copy of AgentDialogue/AgentActions

AgentBridge does **not** duplicate AgentDialogue's or AgentActions' validation logic for spawn/teleport/speak/etc.
It only:

1. Reads state via the same read-only manager calls AgentDialogue/AgentFeed already use
   (`PlayerManager.GetAllOnline()`, `LandblockManager.GetLoadedLandblocks()` + `GetAllWorldObjectsForDiagnostics()`).
2. Appends JSON lines to the **exact same files** AgentDialogue and AgentActions already poll:
   - `agent/dialogue/outbox.jsonl` (AgentDialogue's `PatchClass.PollOutbox`)
   - `agent/actions/inbox.jsonl` (AgentActions' `PatchClass.PollInbox`)

**AgentBridge working correctly for anything it POSTs depends entirely on AgentDialogue and/or AgentActions also
being enabled.** If AgentDialogue is off, a `POST /dialogue/{wcid}/reply` still appends a line to
`outbox.jsonl` and returns `202 queued` - but nothing will ever speak it, because nothing is polling that file. If
AgentActions is off, the same is true of `POST /actions` and `actions/inbox.jsonl`. AgentBridge has no way to know
whether the other mods are enabled and does not check - it is a convenience layer over files those mods own, not a
replacement for them. If all Tom wants is the file contract with zero network exposure, AgentDialogue/AgentActions
alone (with an outside process watching/writing the files directly) already do that - AgentBridge is optional.

## Endpoints

Every endpoint requires the header `X-Agent-Secret: <Settings.SharedSecret>`. Missing or wrong -> `401`, with no
further processing (the body isn't even read).

- **`GET /state`** - deliberately minimal, matching the plan's "no character stats/inventory/position beyond
  landblock" requirement:
  ```json
  {
    "time": 1234567890.0,
    "players": [ { "name": "SomeCharacter", "landblock": "0163" } ],
    "watched": [ { "wcid": 12345, "loaded": true, "landblock": "0163" } ]
  }
  ```
  `players` is character name + landblock only (from `PlayerManager.GetAllOnline()`) - **never** account name, IP,
  stats, inventory, or exact position. `watched` iterates `Settings.WatchWcids` (empty by default, set separately
  from AgentDialogue's/AgentActions' own wcid lists) and reports whether a loaded instance exists
  (`LandblockManager.GetLoadedLandblocks()` + `GetAllWorldObjectsForDiagnostics()`, same enumeration AgentDialogue/
  AgentActions use) and its landblock, or `loaded: false, landblock: null` if none is loaded.

- **`POST /dialogue/{wcid}/reply`** body `{"text":"..."}` - appends `{"npc_wcid":<wcid>,"text":"<cleaned text>"}` to
  `Settings.DialogueOutboxFile` (default `agent/dialogue/outbox.jsonl`), the exact shape
  `AgentDialogue/PatchClass.cs`'s `PollOutbox` already parses (`npc_wcid`, `text`). Text is control-character-stripped,
  trimmed, and capped at `Settings.MaxReplyTextLength` (default 500, mirroring AgentDialogue's own `MaxLength`
  default) before being written - AgentDialogue re-cleans/re-caps it again independently on its side regardless.
  Returns `202 {"status":"queued"}` once the line is appended - this is **not** confirmation it was spoken; that
  depends on AgentDialogue being enabled with `wcid` on its `WatchedWcids` and an instance of it currently loaded.
  `400` if `wcid` in the path isn't a valid nonzero uint, or if `text` is missing/empty after cleaning.

- **`POST /actions`** body = any JSON object, forwarded verbatim (re-serialized from the parsed `JsonDocument`, so a
  stray newline in the body can never split into two inbox lines). Appends one line to
  `Settings.ActionsInboxFile` (default `agent/actions/inbox.jsonl`). AgentBridge does **not** validate the action
  itself - not the `type`, not any allowlist, nothing - that is entirely AgentActions' job when it next polls (see
  `AgentActions/PatchClass.cs`'s `PollInbox`/`HandleSpeak`/`HandleBroadcast`/`HandleSpawn`/`HandleTeleportNpc`).
  AgentBridge only confirms the body is well-formed JSON (`400` if not, or if the body exceeds the size cap).
  Returns `202 {"status":"queued"}` regardless of what AgentActions will later accept or refuse - check
  `agent/actions/log.jsonl` for the real outcome.

Any other path or method -> `404`. Any unexpected exception inside a handler -> `500` with a generic
`{"error":"internal error"}` body - the real exception is logged server-side via `ModManager.Log`, never sent to
the client (no stack traces ever leave the process).

## Binding

`HttpListener` prefixes are **exactly** `http://127.0.0.1:{port}/` and `http://localhost:{port}/` - both added,
neither optional, and nothing else. Never `+`, `*`, or a real interface/hostname; there is no setting that can
change this (it is not exposed via `Settings.json` - only the port is configurable). Default port `8523`
(`Settings.Port`), chosen to be unlikely to collide with anything else already running; change it in `Settings.json`
if it does.

`System.Net.HttpListener` is a plain BCL class (`System.Net.HttpListener`, part of the .NET runtime, not an ACE
type) - verified it requires no special ACE hosting and no additional package reference beyond what the SDK already
provides. **The ACBuilds server containers are Linux** (`Dockerfile.server`, bash `entrypoint.sh`), not Windows, so
Windows-only concerns like `http.sys` URL-ACL reservations (`netsh http add urlacl`) do not apply here at all - on
Linux, .NET's `HttpListener` uses its own managed socket implementation, and binding to an unprivileged loopback
port (8523 by default; anything >= 1024 needs no elevated capability on Linux) needs no special container
permission beyond the port not already being in use inside that container. This has **not** been exercised inside a
running ACE server container in this repo (not built, not deployed, per instructions) - see "Unverified" below. If
`HttpListener.Start()` throws for any reason (port in use, permission problem), `TryStart` catches it, logs an error
via `ModManager.Log`, and leaves `listener` null - the mod falls back to **nothing running**, never a half-started
listener, and never throws into ACE's mod-load path (`OnWorldOpen` always returns normally either way).

## Threading

All request handling happens off ACE's own threads:

- `OnWorldOpen` starts the listener and hands its `GetContextAsync()` accept loop to `Task.Run` - a single
  background task that only calls `HttpListener.GetContextAsync()` (async, non-blocking) in a loop.
- Each accepted request is itself dispatched to its own `Task.Run` (`HandleRequestSafe`), so one slow client can't
  stall the accept loop or other in-flight requests.
- The only ACE calls made from these background threads are read-only lookups
  (`PlayerManager.GetAllOnline()`, `LandblockManager.GetLoadedLandblocks()`/`GetAllWorldObjectsForDiagnostics()`) -
  the same calls AgentDialogue/AgentFeed/AgentActions already make from their own `Timer` callbacks, which are also
  not the game tick thread. No world object is ever mutated by AgentBridge itself; the two POST endpoints only
  append lines to plain files, exactly like AgentDialogue's/AgentActions' own file-append code (same
  try/catch/lock-per-file pattern, see `AppendLine`).
- Every request handler (`HandleRequestSafe` wrapping `HandleRequest`) is wrapped in try/catch. An unexpected
  exception is logged in full server-side and answered with a generic `500` - it can never propagate into
  `AcceptLoop`, `OnWorldOpen`, or any ACE thread.
- `Stop()` cancels the accept loop's `CancellationTokenSource`, then calls `HttpListener.Stop()` and
  `HttpListener.Close()`. `GetContextAsync()`'s resulting exception on a stopped listener is caught in `AcceptLoop`
  and treated as a normal shutdown, not logged as an error. This is meant to guarantee a mod reload or server
  restart releases the socket cleanly rather than leaking it - not exercised against a live reload here (see
  "Unverified").

## Request size cap

`Settings.MaxBodyBytes` (default 8192 = 8 KB) caps every POST body. `Content-Length` is checked first when present;
the read loop itself also aborts (and returns `413`) the moment total bytes read would exceed the cap, so a request
with no/incorrect `Content-Length` (e.g. chunked transfer) can't bypass the limit by lying about its length.

## No ace_auth / ace_shard access, ever

AgentBridge does not reference `ACE.Database`'s player/account tables at all - it calls `ACE.Server.Managers`
(`PlayerManager`, `LandblockManager`) for in-memory state and `ACE.Server.WorldObjects.WorldObject` only to read
`WeenieClassId`/`Location`/`Name` off already-loaded objects, plus plain `System.IO` file appends. There is no code
path from any AgentBridge endpoint to account or shard data, regardless of what is POSTed to it - `POST /actions`
forwards an opaque JSON blob to a file that only *AgentActions* (a separate mod, with its own strict
allowlist/validation) ever acts on.

## Settings

`Enabled` (false), `Port` (8523), `SharedSecret` (`""` - **must** be set non-blank for the mod to start at all),
`WatchWcids` (empty list, independent of AgentDialogue's/AgentActions' own wcid settings), `DialogueOutboxFile`
(`agent/dialogue/outbox.jsonl`), `ActionsInboxFile` (`agent/actions/inbox.jsonl`), `MaxBodyBytes` (8192),
`MaxReplyTextLength` (500).

No admin command is included - `Settings.json` (enable, port, secret, watch-list) is the entire admin-facing
surface, so there is nothing to check against `%TEMP%\cmds.txt` for a name collision.

## Unverified

- Compilation (not built, per instructions).
- `HttpListener.Start()` actually succeeding inside the specific process/account ACE's Docker image runs the server
  as, and whether that account needs any additional Windows HTTP.sys configuration beyond the loopback-exemption
  behavior described above - not exercised against a live container. If it fails, `TryStart`'s catch block logs the
  error and the mod simply doesn't listen (see "Binding" above) - that fallback path itself also hasn't been
  exercised against a real failure in this environment.
- Clean socket release on `Stop()`/mod reload/server restart in practice - the `CancellationTokenSource` +
  `HttpListener.Stop()`/`Close()` sequence follows the documented .NET pattern, but has not been run against a live
  reload here.
- Behavior under concurrent requests at real volume - the per-request `Task.Run` dispatch has no upper bound on
  concurrent in-flight requests (no semaphore/queue depth limit), which is fine for a private, low-traffic,
  loopback-only tool but would not be a reasonable default if this were ever exposed more broadly (it must not be -
  see "READ THIS BEFORE ENABLING").
- Whether `Player.Location` can be null for an online player in some edge state (defensively handled with `?? 0`
  the same way AgentDialogue's inbox write already does, but not exercised against every possible player state).
