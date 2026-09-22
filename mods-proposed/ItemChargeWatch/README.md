# ItemChargeWatch (ACE mod)

Player command that lists every equipped item running on mana (wands, staves, trinkets, some jewelry) with its
current/max mana, sorted lowest-percent-first, so a caster can see at a glance what is about to run dry mid-fight
instead of discovering it only when a cast silently fails.

| Command | Access | What it does |
|---|---|---|
| `/manawatch` | Player | Lists each equipped item with `ItemMaxMana` set: name, current/max mana, percent remaining. Sorted lowest-percent-first. Flags any item at or under `WarnPercent` as `[LOW]`. |

## What it does / doesn't do

- Read-only: never writes `ItemCurMana`/`ItemMaxMana`, never touches any item. Pure property reads, same shape
  as the already-shipped `BurdenCheck`/`LuminanceLedger`.
- Only lists items where `ItemMaxMana.HasValue && ItemMaxMana.Value > 0` - an item with no max mana isn't
  mana-driven at all and would otherwise show a meaningless `0/0` or force a divide-by-zero on the percent.
- A missing `ItemCurMana` (null) is treated as `0` for display, matching how the ACE UI itself shows an item
  that has been fully drained (see `ManaStone.cs`'s own `?? 0` handling of `ItemCurMana` on a target item).
- Distinct from `ManaStone` (a manual refill tool that consumes items) and from `LuminanceLedger` (idea 61, the
  player's own XP-derived augmentation currency) - this is a status readout only, mana-only in scope; it doesn't
  touch vitae, PK status, or burden (`/vitae`, `/pkstatus` already cover those, already shipped).
- Settings: `WarnPercent` (default 20) - the percent-remaining at or under which an item is flagged `[LOW]`.

## Verification (this round)

Fetched fresh from ACE master, `Source/ACE.Server/WorldObjects/ManaStone.cs`:

- `ItemCurMana` / `ItemMaxMana` are read and written on an **external** `target`/`item` object of type
  `WorldObject`, from inside `ManaStone`'s own class body - e.g.
  `target.ItemCurMana.HasValue`, `target.ItemMaxMana.Value`,
  `player.EquippedObjects.Values.Where(k => k.ItemCurMana.HasValue && k.ItemMaxMana.HasValue && k.ItemCurMana < k.ItemMaxMana)`,
  `item.ItemCurMana += adjustedRation`.
  This confirms both properties are plain **public** get/set members declared on `WorldObject` and readable
  from a different declaring class (`ManaStone`), satisfying this repo's external-access verification rule.
  (The properties themselves are declared in `WorldObject_Properties.cs`, as `int?` backed by
  `PropertyInt.ItemCurMana`/`PropertyInt.ItemMaxMana` - the same file/pattern already verified for
  `WorldObject.Value` in `NetWorthCheck`'s README.)
- `player.EquippedObjects.Values` (`Creature_Equipment.cs`, base of `Player`) is the same public collection
  already used identically by `ManaStone.cs` itself, and by the already-shipped `NetWorthCheck` mod.
- `cmds.txt` (327 built-in commands) re-checked this round: `manawatch` is not a built-in command name.

## Risks

- None identified beyond the standard read-only caveat: pure property reads off the caller's own equipped
  items, no interaction with combat, trading, or the database. Same risk profile as `BurdenCheck`/`NetWorthCheck`.

## How to test

1. Build: `bash mods-proposed/check-mod.sh ItemChargeWatch` (must print `MOD OK`).
2. To try in game later (Tom's call - this mod ships `Enabled: false`): flip `Meta.json`'s `Enabled` to `true`
   (and `Settings.json`'s `Enabled` once generated) - place the built folder in the server's `Mods` directory,
   run `mod find`, log in with a caster wearing a mix of mana items at different charge levels, and run
   `/manawatch`. Confirm the list is sorted lowest-percent-first and that any item at or under 20% shows `[LOW]`.

## How to enable later

Same pattern as every other proposed mod here: set `Meta.json`'s `"Enabled"` to `true` (and the generated
`Settings.json`'s `"Enabled"` to `true`), ship the built folder into the server's `Mods` directory (Tom's call,
not automatic - this repo never deploys from `mods-proposed/`).
