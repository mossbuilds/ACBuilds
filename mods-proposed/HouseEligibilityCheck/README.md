# HouseEligibilityCheck

Read-only player command previewing whether the caller currently meets a mansion's
allegiance-rank requirement, before they travel there with money in hand.

`HandleActionBuyHouse` (`Player_House.cs`) only checks this at the moment of purchase and
rejects with `WeenieErrorWithString.YouMustBeAboveAllegianceRank_ToBuyHouse` - there is no
earlier, player-invokable check. This mod reproduces that exact check, read-only.

## Command

`/housecheck` - reads the caller's last-appraised object (same last-appraised pattern as
PriceCheck/VendorStock/CraftForecast: `Player.RequestedAppraisalTarget` + `Player.FindObject`,
since `CommandHandlerHelper.GetLastAppraisedObject()` is `internal static` and not visible
outside `ACE.Server`). If it's a `SlumLord`, reports:

- "no requirement" - `SlumLord.AllegianceMinLevel` is null, or the effective minimum is `<= 0`
- "eligible" - caller's `AllegianceNode.Rank` meets the effective minimum
- "need allegiance rank N, you are M" (or "not in an allegiance") - otherwise

## Source-verified logic

Exact quoted logic from `Player_House.cs`:

```csharp
if (slumlord.AllegianceMinLevel != null)
{
    var allegianceMinLevel = PropertyManager.GetLong("mansion_min_rank", -1).Item;
    if (allegianceMinLevel == -1)
        allegianceMinLevel = slumlord.AllegianceMinLevel.Value;

    if (allegianceMinLevel > 0 && (Allegiance == null || AllegianceNode.Rank < allegianceMinLevel))
    { ...reject... }
}
```

Re-verified directly against `raw.githubusercontent.com/ACEmulator/ACE/master`:

- `SlumLord.AllegianceMinLevel` - `public int?` (`SlumLord.cs`). Confirmed public.
- `AllegianceNode.Rank` - `public uint` field (`Entity/AllegianceNode.cs`). Confirmed public.
- `Player.Allegiance` - `public Allegiance Allegiance { get; set; }` (`Player_Allegiance.cs`).
  Confirmed public by direct declaration, not inferred from its bare use inside `Player.cs`.
- `Player.AllegianceNode` - `public AllegianceNode AllegianceNode { get; set; }`
  (`Player_Allegiance.cs`). Confirmed public by direct declaration, same as above.
- `PropertyManager.GetLong(string, long)` - `public static` (`ACE.Server.Managers`).

Because `Player.Allegiance`/`Player.AllegianceNode` turned out to be genuinely public (not just
usable bare inside `Player.cs`), this mod reads `player.Allegiance` / `player.AllegianceNode.Rank`
directly off the caller's own `Player` instance - the same fields `HandleActionBuyHouse` reads on
itself. `AllegianceManager.GetAllegianceNode(IPlayer)` (used by `AllegianceRoster`,
`FellowshipShareToggle`) was not needed: that helper is for reading a rank from *outside* a
`Player` instance, and a player checking their own rank already has direct access.

The `mansion_min_rank` read keeps the exact server-property-first, weenie-value-fallback order
the game uses, so it never prints a wrong number on a server that overrides the default of 6.

## Settings

None beyond the standard `Enabled` switch (read-only, off by default).

## Risks / unverified

- None outstanding: all three accessibility points named in the brief (`SlumLord.AllegianceMinLevel`,
  `AllegianceNode.Rank`, `Player.Allegiance`/`Player.AllegianceNode`) were re-verified directly
  against the raw ACE master source in this pass, not carried over from the idea's own reasoning.
