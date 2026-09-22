# HouseOverpayRefund

Idea 49 (Round 8). Targets https://github.com/ACEmulator/ACE/issues/1723 ("no check for overpay on
house buying" - "it will take all the items, including all 10M Notes").

## What was actually verified in current ACE master (2026-09-22)

Read `Source/ACE.Server/WorldObjects/Player_House.cs`, `SlumLord.cs`,
`Source/ACE.Server/Network/Structure/HouseProfile.cs` and `HousePayment.cs` directly from
`raw.githubusercontent.com/ACEmulator/ACE/master/...`. The payment math is in
`HousePayment.GetConsumeItems_Inner`, called from `Player.HandleActionBuyHouse` (buy) and
`Player.HandleActionRentHouse` (rent/maintenance) via `Player.GetConsumeItems` ->
`Player.TryConsumePurchaseItems` -> `TryConsumeFromInventoryWithNetworking(item, amount)`.

**The bug is real, but narrower than the issue title suggests:**

- **Plain pyreal coin stacks are NOT bugged.** When a coin stack sent for payment is bigger than
  what's still owed, `GetConsumeItems_Inner` takes the `else` branch and sets
  `consumeAmount = remaining` (not the whole stack). `TryConsumeFromInventoryWithNetworking` then
  removes only that many coins, leaving the rest of the stack behind in the player's pack as a
  reduced (but intact) stack. Overpaying with a pile of loose coins already gives correct change
  today - nothing to fix, and this mod must never touch that path (doing so would double-refund:
  the change is already sitting in the player's inventory as the leftover stack).
- **Trade notes ARE bugged.** A trade note (`WorldObject.IsTradeNote`, e.g. a 10M Note) is a
  discrete, fixed-denomination item - it can't be split. In the same method's trade-note branch,
  when a note's value exceeds what's still owed, the code computes
  `consumeAmount = Ceiling(remaining / baseValue)` - a **count of whole notes** - and consumes that
  many whole notes via `TryConsumeFromInventoryWithNetworking`, destroying their full value even
  though only part of it was owed. That excess value is gone; nothing is ever given back. This
  exactly matches the reporter's "it will take all the items, including all 10M Notes."

**Confirmed: the bug exists in current master**, specifically for trade-note overpayment on house
buy and rent/maintenance (both use the identical `HousePayment.GetConsumeItems_Inner` code path).
It does not exist for pure coin-stack overpayment.

## What was built

A narrow, provably-bounded Harmony patch pair on `Player.HandleActionBuyHouse` and
`Player.HandleActionRentHouse`:

- **Prefix**: before the real payment runs, re-reads the same `HousePayment` the base game is
  about to consume from (`slumlord.GetHouseProfile().Buy`/`.Rent`), and calls the exact same
  `HousePayment.GetConsumeItems(items)` the vanilla code is about to call (a pure, side-effect-free
  read of current inventory - nothing is consumed by this call). From that list it sums how much
  pyreal-equivalent **value** is about to be destroyed (trade notes: `count * StackUnitValue`,
  everything else: the exact amount, which is never in excess per the analysis above) and
  subtracts the amount actually owed (`HousePayment.Remaining`, read *before* the payment call).
  Any positive remainder is stored as a one-shot pending refund for that player guid.
- **Postfix**: after the real payment call returns, checks that the payment actually succeeded
  (house owner now == this player for buy; `slumlord.IsRentPaid()` for rent) before minting
  anything, then creates a new pyreal stack (`WorldObjectFactory.CreateNewWorldObject(273)` +
  `SetStackSize` + `Player.TryCreateInInventoryWithNetworking`, the same pattern as
  KillRace/TreasureHunt) for exactly the pending amount, via an `ActionChain`.

Because the refund is `consumedValue - owed` and `consumedValue` can never exceed the value of the
items actually in the consume list (a subset of what the player sent), the refund is provably
`<= totalSent - owed` - it can never invent money, and it is mathematically zero on the already-
correct coin-stack path, so it never double-pays a player whose change was already returned by
vanilla code.

Off by default (`Enabled: false` in Meta.json and `Settings.json`), per Tom's rule for anything
that touches player currency. `Settings`: `Enabled`, `NotifyPlayer` (system-chat line), `LogRefunds`
(server log line with guid/amount).

No admin command was added - there is nothing here that needs to be verified beyond a login-
character in-game test (buy or rent a house paying with one note larger than the price, confirm
the change stack appears and matches the prefix's computed amount in the log).

## Unverified / not tested

- **Not compiled or run against a live server** (per instructions). The API surfaces used
  (`Player.GetInventoryItems(List<uint>)`, `HousePayment.GetConsumeItems`, `WorldObjectInfo<int>`,
  `WorldObject.IsTradeNote`/`StackUnitValue`, `SlumLord.GetHouseProfile()`,
  `SlumLord.HouseOwner`/`IsRentPaid()`, `Landblock.GetObject(uint)`) were all read directly from
  ACE master source, not compiled against the actual server binaries in this environment.
- The exact display name/existence of a "MMD" (Mega Merchant Deed) item wasn't looked up - the fix
  is written against `WorldObject.IsTradeNote` (`ItemType.PromissoryNote`), which is the general
  class of item the underlying code treats this way, not a specific note's name.
- Multiple simultaneous overlapping buy/rent calls for the same player guid (e.g. two client
  packets racing) could in theory clobber the `Pending` dictionary entry; this is no worse than the
  base game's own non-reentrant assumptions about these handlers, but wasn't stress-tested.
