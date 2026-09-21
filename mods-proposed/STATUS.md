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
IdleKick | READY (compiled; needs in-game test - thread safety + activity detection unverified) | 2026-09-21 | off-by-default idle warn then Session.LogOffPlayer; skips staff, combat, PK timer
TimedMute | READY (compiled) | 2026-09-21 | Admin /mute /unmute /mutes: custom-duration mute over stock gag, JSON list, re-applied at login
SkillRespec | READY (compiled; off by default; test on a throwaway character before enabling) | 2026-09-21 | off-by-default player /respec <skill> [confirm] via Player.ResetSkill, cooldown, pyreal cost, combat refusal
PkNight | READY (compiled; off by default; two-character in-game test needed) | 2026-09-21 | off-by-default scheduled/manual PK window via Player.CheckPKStatusVsTarget postfix (damage rules, no status changes), announcements, Admin /pknight
LoginGreeter | READY (compiled; edit the placeholder rules text before use) | 2026-09-21 | first-login vs returning greeting, online count, first-login tips and /rules over ACE's static server_motd; Enabled default true
ServerPulse | READY (compiled) | 2026-09-21 | read-only Sentinel /pulse [top]: online count, loaded/dormant landblocks, busiest landblocks by players/creatures
WorldBoss | READY (compiled; off by default; boss reward uncapped setting default 0 - keep small) | 2026-09-21 | event: /worldboss start|stop|status spawns ONE boss creature, announces, names top damager, optional pyreal reward, schedule off by default
KillRace | READY (compiled; off by default; reward capped by RewardCap) | 2026-09-21 | event: /killrace start|stop|status, player /racetop, timed kill-count contest with announcements and capped reward
TreasureHunt | READY (compiled; off by default; reward capped by MaxRewardPyreals) | 2026-09-21 | event: riddle hunt via /hunt, /hint gives distance bands only, first player in radius wins; no world objects
HotspotAlert | READY (compiled; off by default; timer only starts if Enabled at load - restart to enable) | 2026-09-21 | throttled admin whispers when a landblock holds too many creatures/players, Sentinel /hotspots, read-only, no files
TriviaNight | READY (compiled; off by default; questions trimmed to 9 - review lore/wording before enabling; prize capped by MaxPyreals) | 2026-09-21 | event: /trivia start|stop|status, first correct chat answer wins a round via Player.HandleActionTalk postfix, scoreboard, optional capped prize
StatCard | READY (compiled) | 2026-09-21 | player /statcard: caller's own level/XP/attributes/vitals/skills; read-only, no files
BugNote | READY (compiled; DataFile path is relative to the server working dir, not the mod folder) | 2026-09-21 | player /bugnote <text> (rate-limited), Sentinel /bugnotes and /bugnoteclear; stores only char name, time, landblock, text
RaiseSkeleton | READY (compiled; off by default; needs in-game test: several CombatPets per owner is not how ACE normally works; SQL in sql/ NOT applied) | 2026-09-21 | player /raiseskel consumes nearest monster corpse -> ACE CombatPet minion up to floor(Self/10) else feral skeleton; /minions, /dismiss
