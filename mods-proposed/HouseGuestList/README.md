# HouseGuestList (proposed, not deployed)

`/myguests`, read-only, Player, off by default (idea 55 in `IDEAS.md`).

## Why
The client's housing panel is the only way today to see who is actually on your guest list, screen by screen.
This closes that gap with a chat command.

## Re-verification against raw source (not just the idea text)
Re-fetched and grepped both files fresh, rather than trusting the idea's citations:

- `Source/ACE.Server/WorldObjects/House.cs` (raw.githubusercontent.com/ACEmulator/ACE/master/...): confirmed
  `public Dictionary<ObjectGuid, bool> Guests;` and `public HashSet<ObjectGuid> StorageAccess => Guests.Where(i => i.Value).Select(i => i.Key).ToHashSet();`
  exactly as the idea quoted them, both genuinely `public` instance members of `House : WorldObject` in
  `namespace ACE.Server.WorldObjects`. This mod reads `Guests` directly (name + storage flag per entry) rather
  than going through `StorageAccess`, since the flag is needed for every guest, not just the storage ones.
- `Source/ACE.Server/Managers/HouseManager.cs`: confirmed `public static List<House> GetCharacterHouses(uint playerGuid)`
  - `public static`, returns `List<House>` as expected, matching `RentReminder`'s existing usage
  (`mods-proposed/RentReminder/PatchClass.cs`) exactly, including calling it with `p.Guid.Full`.
- Player-name lookup: `RentReminder`/`AllegianceRoster` use `PlayerManager.GetOnlinePlayer`, but that misses
  offline guests entirely. Checked `PlayerManager.cs` directly and used `PlayerManager.FindByGuid(uint)` instead,
  which returns `IPlayer?` and covers both online and offline characters (backed by `GetOnlinePlayer`/
  `GetOfflinePlayer` internally) - a closer fit for "who is on the list right now" than an online-only lookup
  would be. `IPlayer.Name` resolves the display name.

No accessibility or return-type mismatches turned up: everything the idea named checked out as written, so
nothing was dropped. The one adjustment is the lookup call (`FindByGuid` instead of the `GetOnlinePlayer` pattern
`RentReminder` uses), made because a guest list command that only shows online guests would silently hide most
of the list.

## What it does
`/myguests` finds the caller's own house(s) via `HouseManager.GetCharacterHouses`, then for each house prints
every entry in `Guests`: the resolved name (or `guid 0x########` if the character has since been deleted and the
name lookup returns null - printed, never an error, per the idea's stated risk) and whether that guest has
storage access (`Guests[guid] == true`) or is visit-only (`false`). A caller with no house is told so; an empty
guest list is reported as empty. Mansions with multiple linked houses (each returned separately by
`GetCharacterHouses`) are listed per house rather than merged, since guest lists are per-house.

## Everything else
Read-only, no `ace_auth`/`ace_shard` access, no world objects touched, no Harmony patches (pure command handler
plus property reads, same shape as `VendorStock`). `myguests` is free against the built-in command list in
`%TEMP%\cmds.txt` (`raise`/`unfreeze`/`resyncproperties` are built-ins there; `order` and `myguests` are not).
Off by default for consistency with every other mod here, even though this is read-only and needs no gate.

## Unverified / not attempted
Not compiled, per instructions - source-verified only (member accessibility and `HouseManager` return type
confirmed against raw GitHub source, not against a local build of `ACE.Server.dll`). Behavior for a house whose
`Guests` dictionary hasn't been populated yet (`BuildGuests()` not yet called on a freshly loaded landblock) was
not traced end-to-end; the code treats a null `Guests` the same as an empty list rather than erroring, but this
was not exercised against a running server.
