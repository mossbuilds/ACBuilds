# MentorRank

Light mentoring. A mentor (level >= MinMentorLevel) invites an online apprentice (<= MaxApprenticeLevel, gap >= MinLevelGap). Off by default, rewards default 0.

## Commands
`/mentor invite <name>`, `/mentor accept`, `/mentor leave`, `/mentor status`, admin `/mentor list`. ("mentor" is not one of ACE's built-in commands.)

## Rewards
When the apprentice levels up past a milestone (default 10/20/30/40/50), the mentor gets `RewardPyreals` (ACE coin stack, capped by MaxPyrealsPerReward) and/or `RewardSkillCredits` (Player.AddSkillCredits, capped), once per pair per level, only if both are in the same fellowship (RequireFellowship) and within MaxDistance in one landblock. Per-mentor hourly cap, per-mentor apprentice cap, re-pair cooldown. Granted on the mentor's own ActionChain.

## Data
`mentorrank-data.json`, keyed by character guid only. Never touches ace_auth or ace_shard, no account data.

## Not done
No XP bonus for the apprentice (patching EarnXP/GrantXP judged riskier than its value); chat announcement only. Same-IP alt farming cannot be detected without account data; fellowship, distance, gap and caps are the mitigation.

## Risks
Private method name `CheckForLevelup` could change; a milestone missed while the mentor is absent is retried at the apprentice's next level-up.
