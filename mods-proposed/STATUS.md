# mods-proposed status (source only, nothing deployed)
XpBoost | READY (compiled) | 2026-09-20 | XP multiplier via Player.EarnXP prefix, /xpboost admin cmd
GentleDeath | READY (compiled) | 2026-09-20 | caps dropped items and vitae penalty, /gentledeath admin cmd
WhereIsEveryone | READY (compiled) | 2026-09-20 | admin /who2, /gotoplayer, /bringplayer
AnnounceEvents | READY (compiled) | 2026-09-20 | scheduled broadcasts, admin /announce start|stop|say
HomeStone | READY (compiled) | 2026-09-20 | player /mark and /gomark with cooldown, PK-timer block
PkGuard | READY (compiled) | 2026-09-21 | blocks PvP damage in listed landblocks via Player.TakeDamage prefix, admin /pkguard
AutoLoot | READY (compiled) | 2026-09-21 | opt-in /autoloot: Corpse.Open postfix moves coins/notes/gems to pack
BuffBot | READY (compiled) | 2026-09-21 | player /buffme: level-6 self buffs, cooldown, off by default
Leaderboard | READY (compiled) | 2026-09-21 | player /top: top levels via PlayerManager.GetAllPlayers plus monster-kill tally (Creature.OnDeath postfix, JSON file)
AdminAudit | READY (compiled; fixed out-param attribute) | 2026-09-21 | logs Advocate+ in-game commands to file via CommandManager.GetCommandHandler postfix, redacts password commands
DeathReport | READY (compiled) | 2026-09-21 | broadcasts fun line on player death, deaths.log, player /lastdeath returns to death spot
RestartWarn | READY (compiled; fixed static settings access) | 2026-09-21 | extra countdown warnings for ACE-scheduled shutdowns plus player /restartwhen; read-only, never shuts down
MilestoneRewards | READY (compiled) | 2026-09-21 | off-by-default skill credits at configured levels via Player.CheckForLevelup postfix, guid-keyed JSON, player /milestones
