# CraftForecast (proposed, not deployed, not compiled)

Read-only player command that previews a combine's success chance **before** the player commits.
Never performs the combine, never consumes or changes any item, never writes files or spawns/
modifies world objects. Off by default (`Enabled: false` in Meta.json and Settings' own `Enabled`).

## Command

`/forecast <target item name>` (any player):
- Source (tool/salvage item) is always the caller's **last-appraised item** - examine it first, then
  run the command. Never named on the command line, per the brief.
- Target is found **by name only in the caller's own inventory or equipment** (pack, side/sub-
  containers, worn items) - never another player's.

Prints: tool/salvage item, target item, the relevant skill and its current trained value,
workmanship (tinkering recipes only), tinkers already applied to the target (tinkering only), and
the resulting success chance as a percent, followed by "This is an estimate; ACE rolls the actual
chance at craft time." A missing recipe is reported plainly ("No recipe found...") instead of an
error.

Free command name checked against `%TEMP%\cmds.txt` (no ACE built-in clash - `raise`, `unfreeze`,
`resyncproperties` are built-ins; `order` is NOT a built-in but wasn't used anyway): `forecast`.
`craftchance` and `tinkchance` are also free, unused, and available as aliases later if wanted.

## How the chance is computed - calls ACE's own methods, does not reproduce them

Both chance functions are `public static` in `RecipeManager` (`ACE.Server.Managers`, verified on
`master`, `RecipeManager.cs`), so CraftForecast **calls them directly** instead of reproducing their
arithmetic:

- `RecipeManager.GetRecipe(Player, WorldObject source, WorldObject target)` (public static,
  verified, line ~32) - looks up the cached cookbook (`DatabaseManager.World.GetCachedCookbook`) or
  falls back to `RecipeManager.GetNewRecipe` (also `public static`, `RecipeManager_New.cs`,
  verified). Both are pure lookups against loaded/cached tables; neither writes anything.
- `RecipeManager.GetRecipeChance(Player, WorldObject source, WorldObject target, Recipe recipe)`
  returns `double?` (public static, verified, line ~159). For a plain (non-tinkering) recipe with no
  difficulty it returns `1.0`; otherwise it reads the player's trained skill
  (`Player.GetCreatureSkill`, public, `Creature_Skills.cs`, verified), applies
  `Player.ConvertToMoASkill` for pre-MoA skills, and runs `SkillCheck.GetSkillChance(skill, recipe.Difficulty)`.
  For a tinkering recipe it dispatches to `GetTinkerChance`.
- `RecipeManager.GetTinkerChance(Player, WorldObject tool, WorldObject target, Recipe recipe)`
  returns `double?` (public static, verified, line ~200): reads `tool.Workmanship`,
  `target.Workmanship`, `target.NumTimesTinkered`, `tool.MaterialType` -> `GetMaterialMod`
  (public static), computes `difficulty` from the documented formula (salvage mod, workmanship mod,
  `TinkeringDifficulty[tinkeredCount]` attempt multiplier - all `public static`/public fields,
  verified), then `SkillCheck.GetSkillChance`. Imbuing recipes (`recipe.IsImbuing()`) divide the
  result by 3 and add the player's `AugmentationBonusImbueChance` bonus, matching ACE exactly.

**Nothing is reproduced by hand** - every number CraftForecast prints comes from calling ACE's own
methods with the caller's real objects, read-only. The only side effects these methods can have on
a failure path are informational chat messages ACE itself sends (`SendWeenieError`, a
`GameMessageSystemChat` for "not trained in that skill") - the same messages a real combine attempt
would send at that point; no `PropertyInt`/`PropertyFloat`/`PropertyBool` is written to the source,
target, or player, and no item is created, destroyed, or consumed. `WorldObject.Workmanship`'s
getter (`WorldObject_Properties.cs`, verified) has one exception: for a small class of items whose
`ItemWorkmanship` value predates a since-fixed formula, simply *reading* Workmanship rewrites that
value into the correct range - this is an existing ACE getter behavior triggered by any appraisal of
such an item (including the vanilla `/appraise`), not something CraftForecast introduces or a
combine-specific mutation.

## Getting the last-appraised item

Same technique as PriceCheck: `CommandHandlerHelper.GetLastAppraisedObject(Session)`
(`ACE.Server.Command.Handlers`) is `internal static class` and not visible from a separate mod
assembly (verified). CraftForecast inlines its three lines instead: `Player.RequestedAppraisalTarget`
(`public uint?`, `Player_Properties.cs`, verified) resolved with
`Player.FindObject(uint, Player.SearchLocations, out, out, out)` (public, `Player_Inventory.cs`,
verified) using `SearchLocations.Everywhere`.

## Own-inventory search

Same as PriceCheck: no ACE method matches inventory items by display name, so CraftForecast walks
`Container.Inventory` (`public Dictionary<ObjectGuid, WorldObject>`, verified) recursively into side
containers, plus `Creature.EquippedObjects` (`public Dictionary<ObjectGuid, WorldObject>`, verified
`Creature_Equipment.cs`), matching `WorldObject.Name` case-insensitively (exact match preferred,
first substring match as fallback).

## Settings

`Enabled` (false) only - no other setting is required; the command is read-only by construction.

## Not implemented from the idea's stretch goal

The idea also mentions salvage-yield preview via `Player.GetStructure(WorldObject, SalvageResults,
ref SalvageMessage)`. This build covers the combine/tinkering success-chance forecast the brief's
"Design" section actually specifies (tool, target, skill, workmanship, tinkers applied, chance,
disclaimer); salvage yield preview was not added and would need its own verification pass against
`Player_Crafting.cs` before being added to this mod or a follow-up one.

## Unverified / assumptions

- `RecipeManager.GetNewRecipe`'s own internals (recipe search across all loaded recipes when no
  cookbook is cached) were not read line-by-line - only confirmed `public static` and that it and
  `GetCachedCookbook` are lookup functions, not writes. If a future ACE version gives either a
  hidden side effect, this mod would inherit it since it calls them directly.
- Whether every recipe's `Skill` value maps to a `Skill` enum member `player.GetCreatureSkill` can
  resolve without throwing was not exhaustively checked across all loaded recipes - a `try/catch`
  around both ACE calls reports a plain message instead of an unhandled exception if one does.
- `TinkeringDifficulty[tinkeredCount]` (public field, 10 entries, index 0-9) has no bounds check in
  ACE's own `GetTinkerChance` for an item tinkered 10+ times; CraftForecast calls the same method
  and would hit the same `IndexOutOfRangeException` ACE itself would on a real combine of such an
  item - caught by the surrounding `try/catch` here rather than crashing the command.
- Not compiled or run against a live server.
