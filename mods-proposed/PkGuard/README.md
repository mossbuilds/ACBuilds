# PkGuard (proposed, not deployed)
Blocks player-vs-player damage inside configured landblocks (safe tutorial/town zones).
Verified in ACE source: `Player.TakeDamage(WorldObject, DamageType, float, BodyPart, bool, AttackConditions)` returns int (Player_Combat.cs), `Position.Landblock` (uint, cell >> 16). Command name `pkguard` is not a built-in.
Settings.json: `Landblocks` - list of hex strings, e.g. `["A9B4"]`.
Command (admin): `/pkguard` shows; `/pkguard add A9B4`, `/pkguard remove A9B4` (runtime only, not saved).
Risks: only physical (melee/missile) damage is blocked; PvP spell damage may use another path and still land. The attacker still gets combat/PK-timer effects. Untested in play.
Test: two PK characters in a listed landblock hit each other, expect 0 damage; outside it, normal. Enable later: copy folder to the mods dir.
