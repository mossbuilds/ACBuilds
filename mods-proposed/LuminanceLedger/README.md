# LuminanceLedger

Read-only `/luminance` (all players): available luminance, maximum luminance (`Player.AvailableLuminance` / `Player.MaximumLuminance`, both `PropertyInt64`-backed), percent to cap, and the two server-wide luminance multipliers (`luminance_modifier`, `quest_lum_modifier`, both via `PropertyManager.GetDouble`). Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure property/method reads.

## Verified against ACE master, re-fetched for this build
- `Source/ACE.Server/WorldObjects/Player_Properties.cs` - `AvailableLuminance` and `MaximumLuminance` are both `public long?` auto-properties backed by `GetProperty(PropertyInt64.AvailableLuminance)` / `GetProperty(PropertyInt64.MaximumLuminance)`, same shape as the already-shipped `TotalExperience`/`AvailableExperience` pair a few lines above in the same file. Matches the idea entry exactly.
- `Source/ACE.Server/Managers/PropertyManager.cs` - `GetDouble(string key, double fallback = 0.0f, bool cacheFallback = true)` is `public static`, confirmed. It returns a `Property<double>` struct, not a bare `double` - this mod reads `.Item` from it, same as ACE's own `Player_Luminance.cs` does.
- `Source/ACE.Server/WorldObjects/Player_Luminance.cs`, `EarnLuminance(long amount, XpType xpType, ShareType shareType)` - confirmed both `PropertyManager.GetDouble("quest_lum_modifier").Item` and `PropertyManager.GetDouble("luminance_modifier").Item` are read there exactly as the idea describes; `quest_lum_modifier` only multiplies in for `XpType.Quest`, `luminance_modifier` applies to all luminance types. Both keys also confirmed present in `PropertyManager.cs`'s `DefaultDoubleProperties` table (default `1.0` each), so the reads always have a real fallback even with no admin override in the DB.

## Not applicable / no gaps found
Every member this mod reads is confirmed public with a fresh source fetch; nothing needed a reflection fallback and nothing was dropped from the idea text.

## Command name
`luminance` checked against `%TEMP%\cmds.txt` (327 built-ins): not present (only `grantluminance`, a different, unrelated built-in, matches on substring).

## Risks
None identified. Pure property reads, no patches, same shape as the already-shipped `StatCard`/`BurdenCheck`.

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/luminance`.
