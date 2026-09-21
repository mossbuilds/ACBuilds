# StatCard

Prints a readable one-screen summary of your own character in chat lines (ACE Discussions "view_readable_stats" idea). Read-only; never touches the databases, never shows other players.

## Command
`/statcard [top]` (any player): name, level, total XP, XP to next level, unspent XP, skill credits, luminance (if any), six attributes, max health/stamina/mana, top trained/specialized skills, deaths.

## Settings (Settings.json)
`TopSkills` (5), `MaxTopSkills` (15), `ShowDeaths` (true).

## Verified against ACE master
Player: `Level`, `TotalExperience`, `AvailableExperience`, `AvailableLuminance`, `AvailableSkillCredits`, `NumDeaths`, `IsMaxLevel`, `GetRemainingXP()` (ulong); Creature: `Strength..Self`, `Health`, `Stamina`, `Mana`, `Skills` dictionary; `CreatureSkill.Current/AdvancementClass/Skill`, `CreatureAttribute.Current`, `CreatureVital.MaxValue` (namespace ACE.Server.WorldObjects.Entity, pulled in by ACE.Server.WorldObjects using? see below). `statcard` is not a stock command.

## Unverified
Nothing known; not compiled yet. `Stamina`/`Mana` verified in Creature_Vitals.cs. Only risk: `SkillAdvancementClass` is assumed to be in ACE.Entity.Enum (used unqualified in Player.cs).
