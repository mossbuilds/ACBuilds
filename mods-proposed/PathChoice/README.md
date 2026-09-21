# PathChoice

ACE has no classes; a path (Necromancer, Rogue, Fighter, Archer, Mage) is a marker chosen with `/path choose <name>`. Disabled by default (`Enabled` false in Settings.json and Meta.json).

## Storage
A real ACE quest stamp `path_<name>` (prefix in `QuestPrefix`) via `player.QuestManager.Stamp/HasQuest/Erase`; ACE persists it in its normal character save. No direct DB access, no JSON files. ACE emotes can gate with InqQuest on `path_necromancer` etc.

## Commands
`/path` (yours + list), `/path choose <name>` (one time; changeable only if `AllowChange`, after `ChangeCooldownHours`), Admin `/path reset <player name>` (online player).

## API for other mods
`PathChoice.PatchClass.GetPath(Player)` returns the lower-case path name or null (null too when the mod is disabled). Alternative with no reference: `player.QuestManager.HasQuest("path_necromancer")`.

## Settings
`Enabled`, `QuestPrefix`, `AllowChange`, `ChangeCooldownHours`, `Paths` (Name, Title, Description, TitleId; 0 = no title).

## Verified against ACE master
QuestManager (ACE.Server.Managers): `HasQuest`, `Stamp`, `Erase`, `GetQuest`; `CharacterPropertiesQuestRegistry.LastTimeCompleted` (uint); `Player.AddTitle(uint, bool)` (ignores undefined ids); `PlayerManager.GetOnlinePlayer`; ActionChain in ACE.Server.Entity.Actions. `path` is not in the built-in command list.

## Unverified
Not compiled. `Player.QuestManager` property name comes from usage in Player_Inventory.cs (Player_Quests.cs 404s). Stamp calls `Update`, so LastTimeCompleted is assumed set by it. Only-Admin check uses `session.AccessLevel`.
