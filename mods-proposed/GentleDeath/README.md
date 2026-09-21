# GentleDeath (proposed, not deployed)
Softer death. Postfix on `Player.GetNumItemsDropped(Corpse)` caps items dropped; prefix on `Player.InflictVitaePenalty(int)` lowers vitae per death (min 1). Both verified in ACE Player_Death.cs.
Command: `/gentledeath [maxItems vitaePercent]` (Admin, runtime only). Settings.json: MaxItemsDropped (2, -1 = no cap), VitaeAmount (2, -1 = vanilla), SkipPkDeaths (true).
Risks: balance; whether corpse.PkLevel is already set when items are rolled is unverified (PK detection may not skip). Test: die on a test char at level 30+, check corpse contents and vitae.
Enable later: copy folder to the server mods dir (Meta.json Enabled).
