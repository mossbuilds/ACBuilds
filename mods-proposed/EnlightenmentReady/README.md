# EnlightenmentReady

Read-only `/enlightencheck` (all players): previews all 5 Enlightenment-quest requirements at once - level 275, 65 luminance-aura credits, society mastery, 25 free pack slots, fewer than 5 uses - plus the player's current enlightenment count (0-5) and the title they'll earn on their next success. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure reads; never calls `Enlightenment.HandleEnlightenment`.

## Corrects Round 8's mistaken drop
Round 8 searched only `ACE.Server.WorldObjects` for a `Player_Enlightenment.cs` file or an "Enlightenment"-named property, found neither, and concluded there was no verified hook. The real hook is `Entity/Enlightenment.cs` (`ACE.Server.Entity`), a static helper class that the Enlightenment NPC itself calls - Round 8 never found it because it never searched `Entity/`.

## Verified against ACE master, fetched in full for this build
`Source/ACE.Server/Entity/Enlightenment.cs`:
- `Enlightenment.VerifyRequirements(Player)` - `public static bool` - the exact code path the NPC calls (`player.Level < 275`, `VerifyLumAugs`, `VerifySocietyMaster`, `GetFreeInventorySlots() < 25`, `player.Enlightenment >= 5`). Not called directly by this mod (it also pushes chat lines to the player) - used only as the source-of-truth definition; each check is re-read individually below so the mod can report all 5 at once instead of stopping at the first failure.
- `Enlightenment.VerifyLumAugs(Player)` - `public static bool` - sums 11 named `player.LumAug*` `int` properties (`LumAugAllSkills`, `LumAugSurgeChanceRating`, `LumAugCritDamageRating`, `LumAugCritReductionRating`, `LumAugDamageRating`, `LumAugDamageReductionRating`, `LumAugItemManaUsage`, `LumAugItemManaGain`, `LumAugHealingRating`, `LumAugSkilledCraft`, `LumAugSkilledSpec`) and checks the total equals 65. Confirmed public static; all 11 properties confirmed public (each assigned to `0` from outside the class in `Enlightenment.RemoveLuminance`, same file).
- `Enlightenment.VerifySocietyMaster(Player)` - `public static bool` - `player.SocietyRankCelhan == 1001 || player.SocietyRankEldweb == 1001 || player.SocietyRankRadblo == 1001`. Confirmed public static.
- `Player.Enlightenment` - `public int` property. Confirmed public: written from *outside* its declaring class as `player.Enlightenment += 1;` in `Enlightenment.AddPerks`, same file - by this repo's external-access rule this proves it is public (a private/protected member could not compile there).
- `Player.GetFreeInventorySlots()` - public instance method on `Player` (`ACE.Server.WorldObjects`), the same member `Enlightenment.VerifyRequirements` itself calls; previously verified public elsewhere in this repo's research and re-confirmed by its use in the master source fetched this round.
- `CharacterTitle.Awakened` / `Enlightened` / `Illuminated` / `Transcended` / `CosmicConscious` - read directly from the `switch (player.Enlightenment)` in `Enlightenment.AddPerks`, same file; this mod maps them off the *current* `player.Enlightenment` value (0-4) to report the title earned on the *next* success, not the title already held.

## Not applicable / no gaps found
Every member this mod reads is confirmed `public` (static helper methods or plain properties) with a fresh full-file fetch; nothing needed a reflection fallback and nothing was dropped from the idea text.

## Command name
`enlightencheck` checked against `%TEMP%\cmds.txt` (327 built-ins): zero matches.

## Risks
None identified. Every method called is a pure boolean/property check already safe for the NPC to call repeatedly (`VerifyRequirements` itself calls all of them on every NPC interaction); this mod never calls `HandleEnlightenment` and makes no state changes.

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/enlightencheck` in game. Expect one line reporting overall READY/not ready, a per-requirement OK/MISSING with the underlying numbers, and the next title on success.

## How to enable later
Flip `Enabled` to `true` in both `Settings.json` and `Meta.json` before deploying (deployment is Tom's decision - this mod is source only, not deployed by this loop run).
