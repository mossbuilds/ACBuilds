# QuestFlagInspector

Idea 74 (Round 12). Read-only Sentinel command that dumps a target player's full quest-flag registry to
the **caller's own chat** - closing a real gap: ACE's own `QuestManager.ShowQuests(Player)` already does
this exact walk, but every line goes to `Console.WriteLine` (the server console window), never to any
player. That makes it useless to a remote admin or a content author standing next to the test character
in-world.

## What it does

`/questflags <player name>` (Sentinel, requires world) - if the target is online, lists every entry in
their quest registry: quest name, current/max solves, whether it is ready now / on cooldown / at max
solves, and when it was last completed. Sorted alphabetically, paginated by `MaxLines`.

## What it does not do

- Never calls `Update`, `Erase`, `SetQuestCompletions`, or `Stamp` - purely reads `GetQuests()`,
  `GetCurrentSolves()`, `GetMaxSolves()`, and `GetNextSolveTime()`.
- Only works for a target who is currently online. `QuestManager.GetQuests()` reads
  `player.Character.GetQuests(player.CharacterDatabaseLock)`, which needs a live loaded `Player` object -
  there is no verified offline/database-only path, so (like this repo's `SquelchAudit`) the command
  reports "not online" rather than guessing.
- Does not touch `ContractTracker` (idea 60)'s `Player.ContractManager` - a different subsystem with its
  own stage/timer fields.

## Verified ACE APIs (fresh full-file fetch, ACEmulator/ACE master)

- `Creature.QuestManager` - public get-only property (`Source/ACE.Server/WorldObjects/Creature.cs`).
  `Player` derives from `Creature`, so `player.QuestManager` is externally accessible.
- `QuestManager.GetQuests()` - public, `Source/ACE.Server/Managers/QuestManager.cs`, returns
  `ICollection<CharacterPropertiesQuestRegistry>` (a clone; "You should not mutate the results").
- `QuestManager.GetCurrentSolves(string)`, `GetMaxSolves(string)`, `GetNextSolveTime(string)` - all
  public, same file, same contract already relied on by this repo's `PathChoice` and `ContractTracker`.
- `QuestManager.ShowQuests(Player)` - public, confirmed to route every line through `Console.WriteLine`
  only, never a `GameMessageSystemChat` - the exact gap this mod closes.
- `PlayerManager.FindByName(string, out bool isOnline)` - public static (already verified by
  `SquelchAudit`), used the same way here.

## Settings

- `Enabled` (bool, default `false`) - master switch, off by default like every mod in this repo.
- `MaxLines` (int, default `40`) - caps how many quest lines one call prints, to avoid flooding chat with
  a very large registry.

## Risks

- None identified for the reads themselves - all four QuestManager calls are pure and already used
  read-only elsewhere in ACE (`ShowQuests`) and in this repo (`PathChoice`).
- A very large registry could flood chat; `MaxLines` paginates it.

## How to test

1. Set `Enabled: true` in `Settings.json` for a local test server (not deployed by this run).
2. As a Sentinel-or-above character, run `/questflags <online test character name>` while that character
   has completed at least one quest (e.g. via `PathChoice`'s `/path choose`).
3. Confirm the registry prints in your own chat window, not the server console, with a solves count,
   ready/cooldown state, and last-completed timestamp per entry.
4. Confirm `/questflags <offline name>` reports "not online" rather than erroring.

## How to enable later

Flip `Enabled` to `true` in `mods-proposed/QuestFlagInspector/Settings.json` (or the deployed mod's
settings file) and restart/hot-reload. Tom decides if and when this ships.
