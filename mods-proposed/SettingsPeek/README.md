# SettingsPeek

Read-only `/serverrules` (all players): lists a fixed allowlist of gameplay properties chosen in Settings.json. Off by default (`Enabled=false` in Settings.json and Meta.json).

## Shows
Only keys named in `BoolKeys`, `LongKeys`, `DoubleKeys` (defaults: pk_server, pk_timer, xp_modifier, quest_xp_modifier, luminance_modifier, drop rates, vitae_penalty, pk_respite_timer, etc.). Never strings, never anything from ACE Config (no DB strings, passwords, tokens, paths, IPs, hosts). No key can be typed by the player; no arguments. Writes nothing, no files, no direct ace_auth/ace_shard access.

## Verified against ACE master
`ACE.Server.Managers.PropertyManager` (public static): `GetBool/GetLong/GetDouble(key, fallback, cacheFallback)` returning `Property<T>` with `.Item`. Cache is a ConcurrentDictionary, safe from a command thread. Command `serverrules` is not in the 327 built-ins.

## Unverified
On a cache miss ACE itself reads ShardConfig (ACE's own code, not ours); all default keys are normally cached at startup. Confirm key names exist on your ACE build.

## Test
Set Enabled=true in Settings.json and Meta.json, run `/serverrules`.
