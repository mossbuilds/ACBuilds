# AllegianceRoster

Player command that shows the caller's own allegiance in chat: monarch, patron, vassal count, member count,
who is online, and (with `all`) every member's level and rank. Read-only, no files, no world objects.
Never shows another allegiance, an account name, an IP, or anything from `ace_auth`. Off by default.

## Commands
- `/roster` - monarch, patron, member/vassal counts, and the list of members currently online with their level.
- `/roster all [page]` - every member (online and, if `ShowOffline`, offline too), paged at `MaxLines` per page,
  online members listed first.
- `/roster vassals` - the caller's own direct vassals only.

`roster` is not a built-in ACE command (checked against the built-in command list captured in `%TEMP%\cmds.txt`
for this task; `raise`, `unfreeze`, `resyncproperties` are built-ins and `order` is not one either). The built-in
`show-allegiances` is an admin dump of every allegiance in the world; this is player-facing and scoped to one
allegiance - the caller's own.

## Settings (Settings.json)
- `Enabled` (false) - master switch, read once in `OnWorldOpen`. Off by default per the brief.
- `MaxLines` (20) - members shown per page of `/roster all`.
- `ShowOffline` (true) - include offline members in `/roster all` (their level still shows; no last-seen).

## Verified against ACE master (raw.githubusercontent.com/ACEmulator/ACE/master/Source/ACE.Server/)
- `Managers/AllegianceManager.cs`: `GetAllegianceNode(IPlayer player)` is `public static`, line 99, returns the
  caller's own `AllegianceNode` (or `null` if not in an allegiance).
- `Entity/AllegianceNode.cs` (namespace `ACE.Server.Entity`): `Monarch`, `Patron` (both `AllegianceNode`,
  readonly), `Vassals` (`Dictionary<uint, AllegianceNode>`), `TotalVassals`, `Rank`, `Player` (`IPlayer`, via
  `PlayerManager.FindByGuid(PlayerGuid)`), `Walk(Action<AllegianceNode>, bool self = true)` (public).
- `Allegiance` class (referenced from `AllegianceNode.Allegiance` and used throughout
  `WorldObjects/Player_Allegiance.cs`): `Members` (dictionary of every node in the allegiance, keyed by guid),
  `TotalMembers`, `MonarchId`. `Source/ACE.Server/Entity/Allegiance.cs` itself 404s on raw.githubusercontent.com
  master (file may have moved/been renamed); its members were confirmed instead from call sites in
  `Player_Allegiance.cs` (`Allegiance.Members.TryGetValue(...)`, `Allegiance.TotalMembers`, `Allegiance.MonarchId`)
  and from `AllegianceNode.cs`'s `Allegiance` field.
- `Entity/IPlayer.cs`: **`Level` (`int?`) is a real member of the `IPlayer` interface** (line 47) - the idea's
  UNVERIFIED note is resolved; it is safe to read `Level` off any `AllegianceNode.Player` (online or offline)
  without casting to `Player`/`OfflinePlayer`. `Name` (`string`) is also on the interface. No last-login/last-seen
  field is exposed on `IPlayer`, so this mod does not show one.
- `PlayerManager.GetOnlinePlayer(ObjectGuid)` (used elsewhere in this repo's finished mods, e.g. `Leaderboard`,
  `MentorRank`) - `null` if the guid is not currently connected; used here purely for the online/offline flag.

## Unverified
- **Allegiance privacy/"hide" setting**: grepped `Player_Allegiance.cs` (1519 lines) for `Hide`/`Private`/`hidden`
  and found nothing resembling a per-member visibility flag on allegiance membership - ACE does not appear to
  have one. This mod does not add its own; it only ever shows the caller's own allegiance, never another one.
- `Entity/Allegiance.cs` could not be fetched directly (404 on master); its `Members`/`TotalMembers`/`MonarchId`
  members are verified only indirectly, through their call sites in `Player_Allegiance.cs` and `AllegianceNode.cs`
  listed above, not by reading the class body itself. If the real member/property names differ slightly, this
  file will need those adjusted before it will compile - not compiled yet.
- `AllegianceNode.Rank` shown per member in `/roster all`/`vassals` is the ACE allegiance rank number (per
  `CalculateRank()`, comment references `asheron.wikia.com/wiki/Rank`), not an in-game officer rank/title -
  worth confirming against `AllegianceOfficerRank` (on `IPlayer`) if Tom wants officer titles instead.
