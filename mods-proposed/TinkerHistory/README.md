# TinkerHistory

Idea 69 (Round 11). Read-only player command: `/tinkerhistory [item name]` lists which materials
have already been tinkered into an item you hold or last appraised, and how many times each.

## What it does

- Uses the existing last-appraised-item pattern (same as `PriceCheck`/`VendorStock`/`CraftForecast`/
  `HouseEligibilityCheck`/`ItemSetPreview`): with no argument it reads `Player.RequestedAppraisalTarget`
  and resolves it via `Player.FindObject`. With an argument it searches only the caller's own
  inventory (pack, sub-containers, equipped items) by name.
- Reads `WorldObject.TinkerLog` (a comma-separated string of `MaterialType` names ACE appends on
  every successful tinker) and parses it with ACE's own `ACE.Server.Entity.TinkerLog` class
  (`Tinkers` list, `NumTinkers(MaterialType)`).
- Prints each distinct material used and its count, or "This item has never been tinkered" if the
  log is null/empty.

## What it does NOT do

- It does **not** compute or claim to show any remaining workmanship bonus or diminishing-returns
  effect. ACE's tinkering-cap formula that consumes this log was not read this round - out of
  scope per the idea's own risk note. This command only reports the raw historical counts ACE
  already keeps for its own internal use.
- No patches, no writes - it never touches `TinkerLog` or any other item state.
- Distinct from the already-shipped `CraftForecast`, which previews odds for a combine that has
  not happened yet. This reads the record of combines that already have.

## Accessibility (re-verified against live ACE master source, 2026-09-22)

- `WorldObject.TinkerLog` - public `string` property, `Source/ACE.Server/WorldObjects/WorldObject_Properties.cs`
  (~line 3080): `public string TinkerLog { get => GetProperty(PropertyString.TinkerLog); set { ... } }`.
  Confirmed public, no reflection needed.
- `ACE.Server.Entity.TinkerLog` - public class, `Source/ACE.Server/Entity/TinkerLog.cs` (fetched in
  full): public constructor `TinkerLog(string csv)`, public field `List<MaterialType> Tinkers`,
  public method `int NumTinkers(MaterialType type)`. All confirmed public, no reflection needed.
- `Player.RequestedAppraisalTarget` and `Player.FindObject` - same accessibility already confirmed
  by the earlier mods that use this pattern (`ItemSetPreview` etc.); not re-verified again here
  since nothing about their signatures changed.
- Nothing in this mod needed a reflection fallback - the whole read path is genuinely public.

## Settings

- `Enabled` (bool, default `false`). Master on/off switch, read once at world open. Off by default
  per repo convention.

## Command

- `/tinkerhistory [item name]` - `AccessLevel.Player`, `CommandHandlerFlag.RequiresWorld`. Command
  name checked against `%TEMP%\cmds.txt` (327 built-ins) - free, no clash.

## Risks

- None identified for the read itself. Read-only, no patches, no state written, no network calls.
- If `TinkerLog` ever contains a material name the running client's `MaterialType` enum doesn't
  recognize, ACE's own `TinkerLog` parser (`Entity/TinkerLog.cs`) prints a `Console.WriteLine`
  warning and skips that entry rather than throwing - this mod does not add its own handling for
  that case since the failure mode already belongs to, and is handled by, ACE's own parser.

## How to test

1. Build via `mods-proposed/check-mod.sh TinkerHistory` - must print `MOD OK`.
2. To try live (not part of this run - source only, not deployed): set `Enabled: true` in
   `Meta.json` (or via the mod's settings file once deployed), tinker an item a few times, then
   `/tinkerhistory` with no argument (after appraising the item) or `/tinkerhistory <item name>`.

## How to enable later

Flip `Enabled` to `true` in the deployed mod's settings (off by default here, per repo convention
for a new, unreviewed mod). No other configuration needed.
