# SkillRespec

Player `/respec <skill> [confirm]`: resets one skill through ACE's own `Player.ResetSkill(skill, refund: true)` (Player_Skills.cs), which updates the client and refunds through ACE's normal path. **OFF by default** (`Enabled=false`).

## Existing ACE behaviour
ACE has no built-in player `/respec`, untrain or skill-reset command (checked AdminCommands.cs and PlayerCommands.cs). The same `ResetSkill` runs from the stock Temple NPC emotes; this mod only adds a gated command.

## Safety
- Enabled=false by default; per-player cooldown (default 168 h, stored in a small JSON file, not the databases); optional pyreal cost (wcid 273).
- Confirmation: repeat the command or add `confirm` within ConfirmSeconds (default 30) for the same skill.
- Refused in combat mode or with an active PK timer.
- Never touches ace_auth / ace_shard directly; work is queued on the player's ActionChain and saved with `SaveBiotaToDatabase`.

## Refunded / not refunded
- Refunded: all XP spent on the skill's ranks (returned as unassigned XP). If Specialized: the specialization credits (Specialized -> Trained). Untrainable "always trained" skills get XP back only.
- Not refunded: the training credits of a Trained skill (it stays Trained), the pyreal cost, and credits for salvage/tinkering skills specialized via augmentation (XP only).
- Only trained/specialized skills can be reset; there is no separate untrain.

## Settings
Enabled, CooldownHours, CostPyreals, ConfirmSeconds, DataFile.

## Risks / test
Changes character data: test on a throwaway character first (check credits and unassigned XP before and after, relog). Refund loops are limited by the cooldown. Enable later by setting Enabled=true and reloading.
