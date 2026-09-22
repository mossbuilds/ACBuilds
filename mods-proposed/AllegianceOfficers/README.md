# AllegianceOfficers

Player command that lists the caller's allegiance officers (Speaker/Seneschal/Castellan) and each one's
effective title (custom, if the monarch set one, otherwise the default rank name). Read-only, no files,
no world objects, no patches.

**Distinct from `AllegianceRoster`** (already shipped): AllegianceRoster walks the whole member tree by
rank number (`/roster`, `/roster all`, `/roster vassals`) and never mentions officer status. This mod reads
only `Allegiance.Officers` - the subset of members holding an officer rank - and their titles; it does not
list rank-and-file members or vassal counts at all.

## Commands
- `/officers` - every officer in the caller's allegiance, highest rank first, with name, numeric rank,
  effective title, and online/offline status. Prints a one-line note if any title has been customized.

## Settings (Settings.json)
- `Enabled` (false) - master switch, read once in `OnWorldOpen`. Off by default per repo convention.

## Verified against ACE master (github-second-brain, ACEmulator/ACE, direct file fetch)
- `Source/ACE.Server/WorldObjects/Allegiance.cs` (`public class Allegiance : WorldObject`):
  - `public Dictionary<ObjectGuid, AllegianceNode> Officers;` - built by `BuildOfficers()`
    (`Members.Where(i => i.Value.Player.AllegianceOfficerRank != null)`).
  - `public bool IsOfficer(ObjectGuid)`, `public bool IsOfficerRank(ObjectGuid, int)`,
    `public bool IsSpeaker/IsSeneschal/IsCastellan(ObjectGuid)` - all public, not used directly by this
    mod (it iterates `Officers` instead) but confirmed public for completeness.
  - `public string GetOfficerTitle(AllegianceOfficerLevel officerRank)` - public, falls back to
    "Speaker"/"Seneschal"/"Castellan" when the matching custom-title property is empty.
  - `public string AllegianceSpeakerTitle/AllegianceSeneschalTitle/AllegianceCastellanTitle` - public,
    `PropertyString`-backed get/set, same pattern as `AllegianceMotd`.
  - `public bool HasCustomTitles` - public, `true` if any of the three custom titles is set.
- `Source/ACE.Server/WorldObjects/Player_Allegiance.cs` (partial `Player`): **the round's flagged caveat -
  `AllegianceOfficerRank`'s own accessibility - is resolved: it is a plain public property**,
  `public int? AllegianceOfficerRank { get => GetProperty(PropertyInt.AllegianceOfficerRank); set { ... } }`,
  same shape as the sibling `public int? AllegianceRank`. No reflection fallback is needed anywhere in
  this mod.
- `Source/ACE.Server/Managers/AllegianceManager.cs` (`public class AllegianceManager`):
  `public static Allegiance GetAllegiance(IPlayer player)` and `public static IPlayer GetMonarch(IPlayer player)`
  - both public static, confirmed by direct fetch this round. This mod uses `GetAllegiance(p)` (the caller's
    own `Player`, which implements `IPlayer`) to get the `Allegiance` object with `Officers` already built.

All of the above were read from the live file content, not inferred from usage elsewhere or from IDEAS.md's
paraphrase.

## Command-name check
`officers` was grepped against the built-in command list captured at `%TEMP%\cmds.txt` (327+ entries) for
this task and is not a built-in. It also does not collide with the already-shipped `AllegianceRoster`
(`roster`), `AllegianceMotd`, or `FellowshipShareToggle` (`fellowshare`) command names.

## Risks
None identified - pure read of an already-built dictionary and public helper methods, no patches, no writes,
no world objects created. A caller with no allegiance, or an allegiance with zero officers, both get a plain
message instead of an empty or malformed list.

## How to test
1. Build: `mods-proposed/check-mod.sh AllegianceOfficers` (must print `MOD OK`).
2. To try in-game later (not part of this task - source only, not deployed): set `Enabled: true` in
   `Settings.json`, copy the mod's output folder to a real ACE server's `/mods` directory, have a monarch set
   at least one officer rank on a vassal (stock allegiance commands), then run `/officers` as any member of
   that allegiance.

## How to enable later
Flip `Enabled` to `true` in `Settings.json` after deployment; off by default here per repo convention.
