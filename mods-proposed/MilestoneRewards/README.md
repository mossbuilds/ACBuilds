# MilestoneRewards

Grants a small number of skill credits when a character first reaches configured levels. Off by default.

## Commands
- `/milestones` (player): shows the next milestone and its reward.

## Settings (Settings.json)
- `Enabled` (default false)
- `LevelToCredits` (default 25/50/75/100 -> 1 credit each)
- `MaxCreditsPerMilestone` (default 3, hard cap)
- `DataFile` (default `milestonerewards-granted.json`): guid -> levels already rewarded, so relog/restart never double-grants. Never touches ace_auth or ace_shard.

## How it works
Harmony postfix on private `Player.CheckForLevelup` (patched by name), then `Player.AddSkillCredits(int)` (both verified in ACE Player_Xp.cs / Player_Skills.cs). Credits are also awarded if a character was already past a milestone when enabled (a one-time catch-up); to avoid that, seed the data file first or set levels above current players.

## Risks
Balance (credits are permanent); catch-up grant on first enable; the private method name could change in an ACE update.

## Test
Enable, set a low level in the map, use `/testlevel`-style admin XP on a test char, check credits and that relog does not repeat.

## Enable later
Copy to mods/, set `Enabled: true`.
