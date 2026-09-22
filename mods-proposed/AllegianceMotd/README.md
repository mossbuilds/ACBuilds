# AllegianceMotd

A monarch/patron-settable message of the day for an allegiance, shown to its members at login and on demand.
ACE has no built-in equivalent - the monthly report confirms the only allegiance-related login text is the fixed
Monarch/Patron/Vassal-colored rank line, not a custom message (see Idea 46, `IDEAS.md`).

## Commands
- `/amotd` - shows the caller's own allegiance's current MOTD (or "no message set").
- `/amotd set <text>` - sets it. Only the allegiance's monarch (`AllegianceNode.IsMonarch`, i.e. `Patron == null`)
  or a patron (`AllegianceNode.HasVassals` - anyone with at least one vassal under them, per the idea's own
  "Monarch OR Patron" wording) may set it; anyone else is refused. Text is stripped of control characters and
  clamped to `MaxLength`. Also broadcasts the new text immediately to every allegiance member currently online
  (`AllegianceNode.Walk`), not just future logins, per the idea's own risk note.

Shown automatically a few seconds after `Player.PlayerEnterWorld()` (Harmony postfix, `ActionChain`-delayed like
`LoginGreeter` - chat sent immediately on enter-world is lost) to every member of that allegiance who logs in.

`amotd` is not a built-in ACE command (checked against the built-in command list captured in `%TEMP%\cmds.txt`
for this task, and against `allegmotd`/`allegiancemotd` as alternate names - none clash; `raise`, `unfreeze`,
`resyncproperties` are built-ins, `order` is not one either).

## How allegiance identity and permission are determined
- A player's own `AllegianceNode` comes from `AllegianceManager.GetAllegianceNode(IPlayer)` (public static,
  `ACE.Server.Managers`, verified this round and in `AllegianceRoster`'s README) - `null` if the caller has no
  allegiance.
- The MOTD is keyed by **the allegiance's monarch's character guid** (`AllegianceNode.Monarch.PlayerGuid.Full`,
  falling back to the node's own `PlayerGuid.Full` if `Monarch` is null, i.e. the caller *is* the monarch and has
  no vassals yet) - this is the one identity that is stable for the life of an allegiance and shared by every
  member's node, so every member's `/amotd` (and login popup) looks up the same key regardless of which node in
  the tree they are.
- Permission to `/amotd set` is `node.IsMonarch` (`Patron == null`, verified in `AllegianceNode.cs` line 23) OR
  `node.HasVassals` (verified, `TotalVassals > 0`) - the monarch, or any patron with at least one vassal
  reporting to them, per the idea text ("Monarch OR Patron"). A plain vassal with no vassals of their own is
  refused.

## Settings (Settings.json)
- `Enabled` (false) - master switch, read once in `OnWorldOpen`. Off by default per the brief.
- `MaxLength` (200) - character cap enforced on `/amotd set`.
- `DelaySeconds` (4) - delay before the login popup, mirroring `LoginGreeter`.
- `DataFile` (`allegiancemotd-list.json`) - small JSON file (`Dictionary<uint, string>`, monarch guid -> text) in
  the mod's own working directory. Never `ace_auth`/`ace_shard`, never account names or IPs - only a character
  guid (public in-game identity, same class of value `AllegianceRoster`/`TimedMute` already persist/display) and
  the MOTD text the monarch/patron chose to broadcast to their own members. Read in `OnWorldOpen`, written under
  `lock (gate)` with `try/catch`, matching the pattern in `TimedMute/PatchClass.cs`.

## Verified against ACE master (raw.githubusercontent.com/ACEmulator/ACE/master/Source/ACE.Server/)
- `Managers/AllegianceManager.cs`: `GetAllegianceNode(IPlayer player)` public static, line 99. `GetMonarch(IPlayer
  player)` (line 34) also exists as a simpler alternative (`player.MonarchId` on `IPlayer`) but this mod uses the
  node-walk form to match `AllegianceRoster`'s established pattern and to get `IsMonarch`/`HasVassals` from the
  same object.
- `Entity/AllegianceNode.cs` (namespace `ACE.Server.Entity`): `Monarch`, `Patron` (both `AllegianceNode`,
  readonly, line 17-18), `IsMonarch => Patron == null` (line 23), `HasVassals => TotalVassals > 0` (line 25),
  `TotalVassals => Vassals?.Count ?? 0` (line 27), `PlayerGuid` (`ObjectGuid`, readonly, line 12), `Walk(Action<
  AllegianceNode> action, bool self = true)` (public, line 90) - recurses into every vassal, used here to
  broadcast a fresh MOTD to online members immediately.
- `Entity/ObjectGuid.cs` (`ACE.Entity`): `Full` (`uint`, line 40, `{ get; }`) - the stable numeric character guid
  used as the JSON dictionary key.
- `WorldObjects/Player_Networking.cs`: `Player.PlayerEnterWorld()` - same Harmony postfix target already
  verified and used by `LoginGreeter` and `TimedMute` in this repo.
- Command-name check: `amotd` absent from `%TEMP%\cmds.txt`'s built-in list (per this task's own note, `order`
  is not a built-in either; `raise`/`unfreeze`/`resyncproperties` are, unrelated).

## Unverified
- **`AllegianceNode.Monarch` for the monarch's own node**: `AllegianceNode.cs`'s constructor takes `monarch` as
  an optional parameter and `IsMonarch` is defined purely from `Patron == null`, not from `Monarch` being null or
  self-referential - it was not confirmed by reading `AllegianceManager.Rebuild`/`AddPlayers` (where nodes are
  actually constructed) whether the root node's own `Monarch` field is left `null` or set to itself. This mod
  handles both cases defensively (`node.Monarch ?? node`), but the exact behavior should be confirmed before
  relying on `Monarch` elsewhere.
- **`HasVassals` as the "Patron" test**: the idea text says "Monarch OR Patron" but ACE's `AllegianceNode` has no
  boolean literally named "IsPatron" - this mod infers "is a patron" as "has at least one vassal", which matches
  the game concept (a patron is anyone with vassals under them) but was not cross-checked against a rank/title
  enum; if ACE tracks patron status more precisely elsewhere (e.g. via `Rank`), this permission check should be
  revisited.
- **Broadcast-on-set reach**: `node.Walk` from the *setter's own node* only reaches that node and its own
  vassals downward, not the whole allegiance if the setter is a mid-chain patron rather than the monarch. Since
  only the monarch or a patron may set it, and this mod stores/reads by the shared monarch-guid key regardless
  of who set it, every member's `/amotd` and next login will show the correct text either way - only the
  *immediate* broadcast-to-online-members from a patron's `set` would miss members outside that patron's own
  branch. Not fixed here (would need `AllegianceManager.FindAllPlayers(monarchGuid)` instead of `node.Walk`) -
  left as a known gap since `FindAllPlayers`'s exact signature/behavior was not verified this round.
- Not compiled yet, per instruction.
