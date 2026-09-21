# XpBoost
Scales XP by Settings.Multiplier via a Harmony prefix on Player.EarnXP.
Command: /xpboost [x] (Admin, also console) shows/sets the multiplier at runtime (not saved).
Settings.json: Multiplier (1.0 off), MaxLevel (0 none), XpTypes (default ["Kill"]).
Risks: balance; Quest XP paths that call GrantXP directly are not boosted. Test: kill a mob at 2x, compare XP. Enable: copy into mods/ and set Enabled.
