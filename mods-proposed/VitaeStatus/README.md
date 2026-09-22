# VitaeStatus

Read-only `/vitae` (all players): current vitae penalty percentage, XP earned back toward clearing it since the last death, and the level vitae was last calculated from. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure property/computed-property reads.

This closes the exact gap Round 9 identified and then dropped: that round's research found `HasVitae` referenced as a bare identifier in `Player_Death.cs` but never located its declaration in any file it had fetched, and gave up on the idea for lack of a confirmed hook.

## Verified against ACE master, re-fetched for this build
- `Source/ACE.Server/WorldObjects/Player_Properties.cs`:
  - `public bool HasVitae => EnchantmentManager.HasVitae;` - confirmed public.
  - `public float Vitae { get { var vitae = EnchantmentManager.GetVitae(); if (vitae == null) return 1.0f; return vitae.StatModValue; } }` (doc comment: "Will return 1.0f if no vitae exists") - confirmed public, exact body matched the idea text.
  - `public int? VitaeCpPool { get => GetProperty(PropertyInt.VitaeCpPool); set { ... } }` - confirmed public, PropertyInt-backed.
  - `public int? DeathLevel { get => GetProperty(PropertyInt.DeathLevel); set { ... } }` - confirmed public, PropertyInt-backed.
  - All four are declared directly on `Player` itself (not accessed-from-elsewhere the way some earlier rounds' finds were) - the cleanest confirmation shape available.
- `Source/ACE.Server/WorldObjects/Player_Death.cs`, `InflictVitaePenalty(int amount = 5)`:
  - `DeathLevel = Level;` and `VitaeCpPool = 0;` are both set on every new death (comments: "for calculating vitae XP" / "reset vitae XP earned"). Confirmed `VitaeCpPool` tracks progress *since the most recent death only*, not a lifetime total - the command's output says "since your last death" to be accurate about this.
- `Source/ACE.Server/WorldObjects/Player_Death.cs`, `PK_DeathTick()`:
  - Confirmed `Player.MinimumTimeSincePk` is a public `double?` property and `"pk_respite_timer"` is a `PropertyManager.GetDouble` key, read together exactly as ACE's own respite-tick logic reads them (`MinimumTimeSincePk < PropertyManager.GetDouble("pk_respite_timer").Item`). Used here only to add one optional note line when the player is also in a PK-respite window, pointing to the already-shipped `PkStatusInfo` for the full picture rather than duplicating its job - this command stays vitae-only.

## Not applicable / no gaps found
Every member this mod reads is confirmed public with a fresh source fetch of both `Player_Properties.cs` and `Player_Death.cs`; nothing needed a reflection fallback and nothing was dropped from the idea text.

## Command name
`vitae` checked against `%TEMP%\cmds.txt` (327 built-ins): not present as a standalone command (only `remove-vitae`, a different, unrelated built-in, matches on substring).

## Risks
None identified. Pure property/computed-property reads, no patches, no write path touched - same shape as the already-shipped `LuminanceLedger`/`BurdenCheck`.

## Test
Set `Enabled=true` in Settings.json and Meta.json, die once to incur vitae, then run `/vitae`. With no vitae active it should print "You have no vitae penalty."
