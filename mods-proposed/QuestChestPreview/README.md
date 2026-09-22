# QuestChestPreview

Read-only player command previewing whether the caller currently meets a quest-gated chest's
requirement, before they travel there. Chest-side twin of `HouseEligibilityCheck` (idea 59), which
does the same job for mansion allegiance rank.

`Chest.CheckUseRequirements(WorldObject activator)` (`WorldObjects/Chest.cs`) only evaluates the
quest gate at the moment of opening - there is no earlier, player-invokable check.

## Command

`/chestcheck` - reads the caller's last-appraised object (same last-appraised pattern as
PriceCheck/VendorStock/HouseEligibilityCheck: `Player.RequestedAppraisalTarget` +
`Player.FindObject`, since `CommandHandlerHelper.GetLastAppraisedObject()` is `internal static`
and not visible outside `ACE.Server`). If it's a `Chest`, reports one of:

- "no quest requirement" - `Chest.Quest` is null
- "you don't have the quest flag yet" - `QuestManager.HasQuest(quest)` is false
- "eligible" - has the quest and `QuestManager.CanSolve(quest)` is true
- "on cooldown, ready in N" - has the quest, can't solve yet, derived from
  `QuestManager.GetNextSolveTime(quest)`
- "solved the maximum number of times" - `GetNextSolveTime` returns `TimeSpan.MaxValue`

## Source-verified accessibility

All re-verified against a fresh full-file fetch of `raw.githubusercontent.com/ACEmulator/ACE/master`
this round (not carried over from the idea's own text):

- `Chest.CheckUseRequirements(WorldObject)` - `public override ActivationResult` (`WorldObjects/Chest.cs`).
- `WorldObject.Quest` - `public string` (`PropertyString.Quest`-backed get/set,
  `WorldObjects/WorldObject_Properties.cs`).
- `QuestManager.HasQuest(string)`, `.CanSolve(string)`, `.GetNextSolveTime(string)` - all `public`
  (`Managers/QuestManager.cs`), same three already used identically by the shipped
  `QuestFlagInspector` (idea 74) and `PathChoice` (idea 27).

## Mutation-safety analysis of `Chest.CheckUseRequirements` (important - checked carefully)

**Not safe to call standalone from a preview command.** Reading the full method body
(`WorldObjects/Chest.cs`) shows it is written to run inline with a real open attempt, not as a
side-effect-free predicate:

1. It calls `base.CheckUseRequirements(activator)` first (WorldObject-level use checks - not
   audited here since the method is never invoked at all by this mod).
2. If `IsLocked`, it sends a transient error and broadcasts `Sound.OpenFailDueToLock` - a real,
   player-visible network side effect - before returning failure.
3. It reads and can mutate open/viewer state: if the chest `IsOpen` by the calling player it calls
   `Close(player)` (closes the chest); if open by another player it either force-closes it (viewer
   not found) or sends that other player nothing but the caller a transient error.
4. Even inside the quest branch itself: `EmoteManager.OnQuest(player)` fires the chest's configured
   on-quest emote set (a real, player-visible effect) on **both** the "player doesn't have the
   quest" and "player can solve" paths, and `QuestManager.HandleSolveError(quest)` sends real
   network chat/error messages to the player on the "on cooldown / max solves" path.

None of that is acceptable to trigger from a read-only preview run before the player has even
walked to the chest - same category of risk flagged for `TryBurnComponents` in
`ComponentPrecheck`, and for the same reason: a method with "check" in its name that is not
actually a pure predicate.

**Chosen fix:** reimplement only the pure-read quest predicate from Chest.cs's own
`if (Quest != null) { ... }` block by hand, calling only `WorldObject.Quest` (a property get) and
`QuestManager.HasQuest` / `.CanSolve` / `.GetNextSolveTime`. All three `QuestManager` methods were
confirmed read-only by reading every line of `Managers/QuestManager.cs`, not just the three named
methods: `HasQuest` -> `GetQuest` (registry lookup only), `CanSolve` -> `GetNextSolveTime`
(registry + cached-quest lookup only), `GetNextSolveTime` (same lookups, arithmetic only) - none of
the three ever call `Update`, `Stamp`, `Increment`, `SetQuestCompletions`, `Erase`, or any other
method on `QuestManager` that writes to the player's quest registry or sends network messages.

## Settings

None beyond the standard `Enabled` switch (read-only, off by default).

## Risks

- None identified for the read itself. `Chest.Quest` will be null for the large majority of
  ordinary loot chests (mostly a `ManaForge`-style or scripted quest chest is the real target
  case) - the command says "no quest requirement" plainly rather than implying every chest is
  quest-gated.
- Does not call `Chest.CheckUseRequirements` at all (see mutation-safety analysis above), so it
  cannot trigger the emote, sound, lock, or open/close side effects that method has.

## How to test

1. Enable in `Meta.json` (`"Enabled": true`) and set `Settings.json`'s `Enabled` to `true` on a
   local dev server.
2. Appraise a quest-gated chest (e.g. a scripted dungeon chest with a `Quest` property set), then
   run `/chestcheck` and confirm the reported state matches the chest's actual `Quest` /
   `QuestManager` state for that character.
3. Appraise an ordinary non-quest chest and confirm "no quest requirement".
4. Appraise a non-chest object and confirm the "isn't a chest" message.
