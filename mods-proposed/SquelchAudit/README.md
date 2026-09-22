# SquelchAudit

Sentinel-only command `/squelchcheck <player name>` reports whether a player
has any active chat squelches - filling the chat-moderation gap Round 11
flagged (found only in the wrong directory that round; the real hook is
`WorldObjects/Managers/SquelchManager.cs`, found in Round 12). Staff currently
have no in-game way to check who a player has squelched, or whether a player
who claims "nobody will talk to me" has in fact globally squelched an entire
channel via `@filter`.

## What it does

- `/squelchcheck <player name>` (Sentinel) looks the player up via
  `PlayerManager.FindByName`. If they are online, it reports:
  - Whether they have any active squelches at all (`SquelchManager.HasSquelches`).
  - The count of squelched characters, squelched accounts, and global-filter
    entries (`SquelchManager.Squelches.Characters/Accounts/Globals.Filters`,
    each a `.Count`).
  - The player's global channel squelch mask (`Player.SquelchGlobal`) printed
    by its flag names (e.g. `Trade, LFG`), or "none".
- Off by default (`Meta.json: "Enabled": false`, `Settings.Enabled = false`).

## What it does NOT do

- It never adds, removes, or modifies a squelch. It never calls any of
  `SquelchManager`'s `HandleActionModify*Squelch` methods.
- It does not list the actual squelched names - only counts. Printing
  individual squelched player names would need a fresh read of `SquelchDB`'s
  own file (its `Accounts`/`Characters` dictionaries store raw guids/account
  ids in network-protocol format, not resolved names) and is out of scope for
  this first version, per the idea's own flagged risk.
- It only works while the target is online. `Player.SquelchManager` is built
  fresh in memory every time a player enters the world
  (`Player.SetEphemeralValues()` calls `SquelchManager = new SquelchManager(this)`)
  - there is no persisted, always-available `SquelchManager` on the
  database-backed `OfflinePlayer`/`IPlayer` path, so an offline lookup reports
  "not online" rather than guessing at a value.

## Accessibility verification (this round, against ACEmulator/ACE master)

All four fetched directly via github-second-brain from `Source/ACE.Server/`:

- `WorldObjects/Managers/SquelchManager.cs`: `public class SquelchManager` in
  `ACE.Server.WorldObjects.Managers`, with `public SquelchDB Squelches;` and
  `public bool HasSquelches => Squelches.Accounts.Count > 0 || Squelches.Characters.Count > 0 || Squelches.Globals.Filters.Count > 0;`.
  Both public, exactly as the idea text claims.
- `WorldObjects/Player.cs`: `public SquelchManager SquelchManager;` - a public
  field directly on `Player`, confirmed by direct fetch of the class body
  (line context: declared alongside `ConfirmationManager`, assigned in
  `SetEphemeralValues()` as `SquelchManager = new SquelchManager(this);`).
- `WorldObjects/Player_Properties.cs`: `public SquelchMask SquelchGlobal { get; set; }`
  (backed by `PropertyInt.SquelchGlobal`) - a public property directly on
  `Player`, confirmed by direct fetch.
- `Managers/PlayerManager.cs`: `BroadcastToChannel(Channel, Player, ...)`
  reads `player.SquelchManager.Squelches.Contains(sender)` directly - this is
  the external-class access this repo's verification rule requires, and it is
  confirmed present in the fetched file (`PlayerManager` is a different class
  than both `Player` and `SquelchManager`). `PlayerManager.FindByName(string, out bool isOnline)`
  is `public static` and returns `IPlayer`, also confirmed by direct fetch.

Nothing needed a reflection fallback - every member this command reads is a
genuine public field/property/auto-property, not `protected`/`private`/`internal`.

`SquelchDB`'s own field names (`Accounts`, `Characters`, `Globals.Filters`)
were only independently confirmed as read *inside* `SquelchManager` itself
(its own constructor and `UpdateSquelchDB`) and *inside* `SquelchDB`'s own
declaration is not fetched this round - but since `SquelchManager.Squelches`
is a public field of type `SquelchDB` and `SquelchDB.Accounts`/`.Characters`/
`.Globals.Filters` are read the same way from `SquelchManager.HasSquelches`'s
own public-facing expression body (which this command also reads, unchanged),
this does not introduce an unverified access path - the command reads exactly
the same properties `HasSquelches` already reads, just individually for their
`.Count`.

## Settings

None beyond the master `Enabled` switch. No server properties are read, and
no squelch is ever added, removed, or modified.

## Risks

None identified for the read path itself. The one intentional scope limit
(online-only, counts-only) is documented above rather than worked around with
a reflection fallback into `OfflinePlayer`/database-backed squelch data,
since no such public accessor was found or verified this round.

## How to test

1. Enable in `Settings.json` (`"Enabled": true`) and deploy to a local dev
   server (not covered by this build - source only).
2. As a Sentinel+, `/squelchcheck <name>` for an online player with no
   squelches -> "No active squelches..." and "Global channel squelch
   (@filter): none."
3. Have that player squelch another character (`/squelch <name>`, a stock
   client command) and re-run -> "Squelched characters: 1".
4. Have that player run `@filter` on a channel and re-run -> the global mask
   line lists that channel by name.
5. `/squelchcheck` an offline character name -> "is not online" message, no
   error or stale data.
6. Confirm no squelch state changes across any of the above - the command
   never writes anything.
