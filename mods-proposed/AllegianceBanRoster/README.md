# AllegianceBanRoster

Monarch-only, read-only command that lists an allegiance's ban list and pre-approved-vassal list -
previously unreviewable in-game without re-attempting a (un)ban against a specific name. Read-only, no
files, no world objects, no patches, never adds or removes a ban or an approved vassal.

## Commands
- `/allegianceban` - if the caller is the monarch of their allegiance, prints the full ban list and the
  full pre-approved-vassal list, each guid resolved to a character name via `PlayerManager.FindByGuid`
  (falling back to the raw guid for a deleted character). Anyone else gets a plain refusal message.

## Settings (Settings.json)
- `Enabled` (false) - master switch, read once in `OnWorldOpen`. Off by default per repo convention.

## Verified against ACE master (github-second-brain, ACEmulator/ACE, direct file fetch)
- `Source/ACE.Server/WorldObjects/Allegiance.cs` (`public class Allegiance : WorldObject`):
  - `public AllegianceNode Monarch;` - public field, used for the monarch-only gate
    (`allegiance.Monarch.PlayerGuid.Full == p.Guid.Full`, the same `.Full` equality
    `Allegiance.Equals` itself uses).
  - `public Dictionary<uint, PropertiesAllegiance> BanList => Biota.PropertiesAllegiance.GetBanList(BiotaDatabaseLock);`
  - `public Dictionary<uint, PropertiesAllegiance> ApprovedVassals => Biota.PropertiesAllegiance.GetApprovedVassals(BiotaDatabaseLock);`
  - `public bool IsBanned(uint playerGuid)`, `public bool HasApprovedVassal(uint playerGuid)` - both
    public, confirmed, not used directly (the command reads the two dictionaries' keys instead, since it
    needs to enumerate every entry, not test one).
  - `public void AddBan(uint playerGuid)`, `public void AddApprovedVassal(uint playerGuid)` - public,
    confirmed, **not called anywhere in this mod** - it is read-only by design; use the stock allegiance
    ban/unban commands to change either list.
- **Round's flagged caveat, resolved**: `PropertiesAllegiance` is `ACE.Entity.Models.PropertiesAllegiance`
  (`Source/ACE.Entity/Models/PropertiesAllegiance.cs`, fetched in full this run):
  ```csharp
  public class PropertiesAllegiance
  {
      public bool Banned { get; set; }
      public bool ApprovedVassal { get; set; }
  }
  ```
  Both per-entry fields are plain public auto-properties - no reflection fallback is needed anywhere in
  this mod. In practice every entry already in `BanList` has `Banned == true` (that's how
  `GetBanList` filters) and every entry in `ApprovedVassals` has `ApprovedVassal == true`, so re-printing
  the flag per entry would only repeat which dictionary the guid came from - the command labels each name
  by its section header instead.
- `PlayerManager.FindByGuid(uint)` (`ACE.Server.Managers`, public static) - covers online and offline
  characters, same call `HouseGuestList` and `AllegianceOfficers`'s sibling mods already use; falls back
  to a `(deleted character, guid N)` label if the character record is gone, matching `HouseGuestList`'s
  pattern exactly.
- `AllegianceManager.GetAllegiance(IPlayer)` (`ACE.Server.Managers`, public static, same call
  `AllegianceOfficers` uses) - `Player` implements `IPlayer` (`ACE.Server.Entity`).

All of the above were read from the live file content, not inferred from usage elsewhere or from
IDEAS.md's paraphrase.

## Command-name check
`allegianceban` was grepped against the built-in command list captured at `%TEMP%\cmds.txt` (327 entries)
for this task and is not a built-in, and does not collide with `AllegianceRoster` (`roster`) or
`AllegianceOfficers` (`officers`).

## Access model
`AccessLevel.Player` (matching the sibling `AllegianceRoster`/`AllegianceOfficers` mods' convention),
gated in-command by `allegiance.Monarch.PlayerGuid.Full == player.Guid.Full`. A non-monarch member gets a
plain refusal, not a stripped-down view - the ban and approved-vassal lists are not member-visible
information in stock ACE either.

## Risks
None identified beyond the standard caller-has-no-allegiance / caller-is-not-monarch guards, both handled
with a plain message. Purely reads two already-built dictionaries off the `Allegiance` biota; no writes,
no patches, no world objects created.

## How to test
1. Build: `mods-proposed/check-mod.sh AllegianceBanRoster` (must print `MOD OK`).
2. To try in-game later (not part of this task - source only, not deployed): set `Enabled: true` in
   `Settings.json`, copy the mod's output folder to a real ACE server's `/mods` directory, have a monarch
   ban a character and pre-approve another vassal with the stock commands, then run `/allegianceban` as
   the monarch (should list both) and as a non-monarch member (should be refused).

## How to enable later
Flip `Enabled` to `true` in `Settings.json` after deployment; off by default here per repo convention.
