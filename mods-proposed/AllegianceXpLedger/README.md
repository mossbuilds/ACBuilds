# AllegianceXpLedger (ACE mod)

Player command that reports the caller's own allegiance-XP accounting - a facet of the patron/mentor system
no prior idea (85 mods before this one) had touched. Distinct from the already-shipped `MentorRank` (idea 34,
a mod-owned kill-tally JSON, no relation to ACE's real allegiance-XP flow) and from
`AllegianceRoster`/`AllegianceOfficers`/`AllegianceMotd`/`AllegianceBanRoster` (all membership/role/messaging,
none touch XP).

| Command | Access | What it does |
|---|---|---|
| `/allegiancexp` | Player | Reports your `AllegianceXPGenerated`, `AllegianceXPCached`, and `AllegianceXPReceived` in one line. |

## What it does / doesn't do

- Read-only: never writes any of the three properties, never calls `AddAllegianceXP()` or `GrantXP`.
- Reports exactly three raw property reads on the caller's own `Player` object - no math, no estimate.
- `AccessLevel.Player`, `RequiresWorld` (must be logged in and in the world). No settings; nothing to configure.

## Source verification (this round, fresh fetch of ACE master)

All three named members re-verified directly against `ACEmulator/ACE`,
`Source/ACE.Server/WorldObjects/Player_Allegiance.cs` (fetched in full via `raw.githubusercontent.com`, not
guessed from the idea text):

```csharp
public ulong AllegianceXPCached      // line 23
{
    get => (ulong)(GetProperty(PropertyInt64.AllegianceXPCached) ?? 0);
    set { if (value == 0) RemoveProperty(PropertyInt64.AllegianceXPCached); else SetProperty(PropertyInt64.AllegianceXPCached, (long)value); }
}

public ulong AllegianceXPGenerated   // line 29
{
    get => (ulong)(GetProperty(PropertyInt64.AllegianceXPGenerated) ?? 0);
    set { if (value == 0) RemoveProperty(PropertyInt64.AllegianceXPGenerated); else SetProperty(PropertyInt64.AllegianceXPGenerated, (long)value); }
}

public ulong AllegianceXPReceived    // line 35
{
    get => (ulong)(GetProperty(PropertyInt64.AllegianceXPReceived) ?? 0);
    set { if (value == 0) RemoveProperty(PropertyInt64.AllegianceXPReceived); else SetProperty(PropertyInt64.AllegianceXPReceived, (long)value); }
}
```

All three are plain `public` instance properties, declared directly on `partial class Player`, with a normal
get/set - genuinely public and readable from a mod (`player.AllegianceXPCached` etc.), same pattern every
other read-only status mod in this repo uses.

**What each one actually represents** (re-derived by reading the surrounding logic in the same file, not
just the idea pointer, since one of the idea's own wordings needed correcting):

- **`AllegianceXPCached`** - allegiance XP a patron's vassals have generated for them that has **not yet been
  applied**. Applied and zeroed by `AddAllegianceXP()` (line 487-499): `GrantXP((long)AllegianceXPCached, ...)`,
  then `AllegianceXPReceived += AllegianceXPCached`, then `AllegianceXPCached = 0`. `AddAllegianceXP()` is
  called from `HandleAllegianceOnLogin()` (line 442-446) three seconds after the patron next logs in - so a
  nonzero cached amount here means "will be applied on your next login," not "lost."
- **`AllegianceXPReceived`** - cumulative allegiance XP actually applied to the caller from vassals over time
  (incremented every time `AddAllegianceXP()` runs, line 497). This one is a genuine running lifetime total;
  nothing in the file resets it.
- **`AllegianceXPGenerated`** - **corrected from the idea text**, which called this "lifetime XP this character
  has generated up through its patron chain." Reading the file in full shows it is reset to `0` at line 130,
  inside the swear-allegiance handler, every time the caller swears an oath to a (new) patron - so it tracks
  XP generated **since the caller's current oath**, not a true lifetime total. This mod's wording and the
  command's own output reflect that correction (`"generated (since your current oath)"`), rather than
  repeating the idea text's inaccurate "lifetime" framing.

`AllegianceManager.GetAllegianceNode`/`AllegianceNode.Rank` (verified in an earlier round) were considered for
an optional rank-context line but left out: the three XP properties alone already answer the "admin-only or
invisible number" gap this idea targets, and adding rank context would pull in `AllegianceNode`, which can be
`null` for a player with no allegiance at all (`HasAllegiance` checks exactly that) - out of scope for a
one-line ledger report and not re-verified this round.

## Command-name check

`allegiancexp` confirmed absent from the 327 built-in command names in `%TEMP%\cmds.txt` before coding.

## Risks

- None identified beyond the wording correction above: this is three property reads with no writes, no RNG,
  no combat or trade interaction, and no per-tick cost.
- A player with no allegiance at all will simply see all three values as `0` - `AllegianceXPCached`/`Generated`/
  `Received` are all backed by `GetProperty`, which returns the `?? 0` default rather than throwing when unset.

## How to test

1. Build: `bash mods-proposed/check-mod.sh AllegianceXpLedger` (must print `MOD OK`).
2. To try in game later (Tom's call - this mod ships `Enabled: false`): flip `Meta.json`'s `"Enabled"` to
   `true`, place the built folder in the server's `Mods` directory, run `mod find`, log in as a character with
   an active allegiance (ideally both a patron with online vassals and a vassal), and run `/allegiancexp`.
   Confirm the pending amount shown matches what appears added to XP on the next login, and that the reported
   generated amount resets to 0 immediately after swearing a fresh oath.

## How to enable later

Same pattern as every other proposed mod here: set `Meta.json`'s `"Enabled"` to `true`, ship the built folder
into the server's `Mods` directory (Tom's call, not automatic - this repo never deploys from `mods-proposed/`).
