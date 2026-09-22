# AccountStash

A shared pyreal stash across every character on one account. Off by default (`Enabled=false`).

## Why this is safe (no ace_auth/ace_shard)

The idea (Round 8, #50 in `mods-proposed/IDEAS.md`) already scopes itself correctly: it does not ask for
cross-account or cross-character DB access. The per-account key is `Session.Account`
(`ACE.Server.Network.Session`, verified against ACE master: `public string Account { get; private set; }`,
set via `SetAccount()` at login) - a value ACE already computed and handed to the live session, never a row
read from `ace_auth` or `ace_shard`. The balance itself is stored in a mod-owned JSON file
(`AccountStash_balances.json` by default, `Account -> long pyrealBalance`), written and read only by this
mod's own file I/O. No database connection, no `ace_auth`/`ace_shard` table, no other account's or
character's data is ever touched.

Only pyreals are handled, as the idea note requires - luminance, legendary keys and sturdy iron keys are
explicitly out of scope because they need item-identity handling (which key, which enchantment state) this
mod does not attempt.

## Command

`/stash deposit <amount>` - consumes pyreals from the caller's own inventory
(`Player.TryConsumeFromInventoryWithNetworking(uint wcid, int amount)`, verified in
`Source/ACE.Server/WorldObjects/Player_Inventory.cs`) and adds to the account's stash balance.

`/stash withdraw <amount>` - subtracts from the account's stash balance and mints a fresh pyreal stack into
the caller's own inventory via `WorldObjectFactory.CreateNewWorldObject(273)` +
`Player.TryCreateInInventoryWithNetworking` (the same pattern `HouseOverpayRefund` already uses in this
repo). If the mint or the inventory-add fails (pack full), the stash is refunded and nothing is lost.

`/stash balance` - reports the account's stash balance. Read-only.

`stash` was checked against the built-ins list (`%TEMP%\cmds.txt`) and is not present.

## Concurrency

Every read-modify-write of the balance file happens under one process-wide lock (`FileLock`). The idea note
asked for a per-account lock; a single shared JSON file still serializes on its own I/O regardless of how
finely the in-process lock is sliced, so one lock is simpler and gives the same guarantee. A short per-account
cooldown (`CooldownSeconds`, default 5s) additionally rejects a second `/stash` call for the same account
before the previous one's chain has had a chance to run, covering the case the idea called out - two
characters on the same account online at once racing a deposit/withdraw.

`deposit`/`withdraw` reject amounts that would take the balance negative or over `MaxBalance` and refund the
player rather than destroying pyreals on any failure path (mint failure, full pack, over-max deposit).

## Settings.json

`Enabled` (false), `BalanceFile`, `MaxBalance` (250,000,000), `CooldownSeconds` (5), and the message texts.

## Unverified

Not compiled. `WorldObjectFactory.CreateNewWorldObject` and `Player.TryCreateInInventoryWithNetworking` are
verified by direct match against `HouseOverpayRefund`'s already-reviewed usage in this repo and against ACE
master source, but this mod's own build has not been run. The balance file's directory (relative to the
mod's working directory) has not been confirmed writable inside the server container - if `/mods/AccountStash`
is not writable, `BalanceFile` should be pointed at a writable path in `Settings.json`.
