# HouseHookMeter (proposed, not deployed)

`/myhooks`, read-only, Player, off by default (idea 56 in `IDEAS.md`).

## Why
The client UI buries the used-vs-cap hook count, and the server can disable the whole hook-limit
system with one setting (`house_hook_limit`) that has no in-game readout either way. This closes
both gaps with a chat command.

## Re-verification against raw source (not just the idea text)
Re-fetched and grepped `Source/ACE.Server/WorldObjects/House.cs` fresh from
raw.githubusercontent.com/ACEmulator/ACE/master/..., rather than trusting the idea's quotes:

- `public int HouseMaxHooksUsable { get => GetProperty(PropertyInt.HouseMaxHooksUsable) ?? 25; ... }` -
  confirmed verbatim, genuinely `public` instance property of `House : WorldObject` in
  `namespace ACE.Server.WorldObjects`.
- `public int HouseCurrentHooksUsable { get => GetProperty(PropertyInt.HouseCurrentHooksUsable) ?? HouseMaxHooksUsable; ... }` -
  confirmed verbatim, same class.
- `public List<Hook> Hooks { get => ChildLinks.OfType<Hook>().ToList(); }` - confirmed verbatim, same class.
- `HouseManager.GetCharacterHouses(uint playerGuid)`: confirmed `public static`, returns `List<House>`,
  matching `HouseGuestList`'s existing usage exactly - reused the identical call.
- `PropertyManager.GetBool(string key, bool fallback, bool cacheFallback)`: confirmed `public static`
  in `ACE.Server.Managers`, returns a `Property<bool>` struct with a `.Item` member - reused the exact
  pattern `SettingsPeek` already ships (`cacheFallback: false` so this read-only command doesn't add
  cache entries for a key it only reads once per call).

Everything the idea named checked out exactly as written - no type or accessibility mismatches, nothing
dropped or adjusted. `House.HookGroupLimits`/`GetHookGroupCurrentCount`/`GetHookGroupMaxCount` also exist in
the same file (a finer-grained per-category limit system) but the idea only asked for the overall used/cap/
physical numbers, so this mod doesn't reach for them.

## What it does
`/myhooks` finds the caller's own house(s) via `HouseManager.GetCharacterHouses`, reads
`house_hook_limit` once via `PropertyManager.GetBool` (default `true`, matching the wiki), then for each
house prints:
- hooks in use vs. usable cap (`HouseCurrentHooksUsable` / `HouseMaxHooksUsable`) - if `house_hook_limit`
  is off server-wide, the cap number is shown but the line says plainly that it isn't enforced, per the
  idea's stated risk, instead of printing a number that would mislead the player;
- the count of hook fixtures physically present (`Hooks.Count`), separate from the usable cap, since a
  house can have more physical hook fixtures than the current usable-hook limit allows filling.

A caller with no house is told so. Mansions with multiple linked houses (each returned separately by
`GetCharacterHouses`) are reported per house, prefixed with the house name, rather than merged/summed.

## Everything else
Read-only, no `ace_auth`/`ace_shard` access, no world objects touched, no Harmony patches (pure command
handler plus property reads, same shape as `HouseGuestList`/`SettingsPeek`). `myhooks` is free against the
built-in command list in `%TEMP%\cmds.txt` (`raise`/`unfreeze`/`resyncproperties` are built-ins there;
`order`/`myguests`/`myhooks` are not). Off by default for consistency with every other mod here, even
though this is read-only and needs no gate.

## Unverified / not attempted
Not compiled, per instructions - source-verified only (member accessibility and return types confirmed
against raw GitHub source, not against a local build of `ACE.Server.dll`). Behavior for a house whose
`Hooks`/`ChildLinks` collection hasn't been fully populated yet on a freshly loaded landblock was not
traced end-to-end; the code simply reads whatever `Hooks.Count` returns at call time rather than forcing
a rebuild, and this was not exercised against a running server.
