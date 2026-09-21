# RentReminder

Read-only. Off by default (`Enabled=false`). Tells a player about THEIR OWN house rent only, at login (delayed, one reminder per cooldown) and via `/rent` (name checked free in the built-ins list: rent, houserent, rentdue).

## How the due time is read
`HouseManager.GetCharacterHouses(player.Guid.Full)` (ACE.Server.Managers, public static, reads ACE's in-memory rent queue) gives the caller's houses; `House.GetRentDue(uint purchaseTime)` (public, ACE.Server.WorldObjects; 30-day periods, 90 for apartments) with `player.HousePurchaseTimestamp` (public). Soonest house is shown, in UTC. `SlumLord.IsRentPaid()` is not used: the paid state needs the slumlord's inventory loaded, so the message is generic ("pay the maintenance") rather than claiming paid/unpaid. No player names, accounts or IPs are sent; no database access; nothing is paid or changed.

## Settings.json
`Enabled`, `WarnAtLogin`, `WarnDays` (3), `DelaySeconds` (5), `CooldownMinutes` (60), and message texts (`{0}` time left, `{1}` due date UTC; braces as `{{ }}`). Login warns only under WarnDays; `/rent` always answers.

## Unverified
Not compiled. Rent queue may be empty for offline-loaded houses (test with a real owner). If GetRentDue is already-passed, message says due now.
