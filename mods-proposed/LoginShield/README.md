# LoginShield

Off by default (`Enabled=false`). Short damage immunity after entering the world (ACE issue #950: arriving into aggro).

- Postfix on `Player.PlayerEnterWorld` starts a per-character shield (default 10 s), keyed by character guid, in memory only. Staff are skipped.
- Prefix on `Player.TakeDamage` (same signature PkGuard patches) returns 0 damage while shielded.
- With `BreakOnAttack` (default true) the shield ends on the player's first melee, missile or targeted spell attack (prefixes on `HandleActionTargetedMeleeAttack`, `HandleActionTargetedMissileAttack`, `HandleActionCastTargetedSpell`). Untargeted spells do not break it, and the timeout still ends it.
- `/loginshield` (Admin): count of shielded characters. Not a built-in name.
- Settings: `ShieldSeconds`, `BreakOnAttack`, `Notify`, `StartMessage`. No files are written.

Left out on purpose: no passwords, hashes, account names, IPs or credentials are read, stored or logged; no ace_auth/ace_shard access; no login check is touched. Shield state is guid-only and lost on restart.

Risks: DoT and spell damage that bypasses `TakeDamage` is not covered; patch order with PkGuard/PkNight is irrelevant (both return 0). Not compiled yet; run `check-mod.sh LoginShield`.
