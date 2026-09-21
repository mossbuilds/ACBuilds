# KillRace (proposed, not deployed)

Timed monster-kill contest. Admin: `/killrace start <minutes> [creatureNameFilter]`, `/killrace stop` (ends and announces), `/killrace status`. Players: `/racetop`.

Settings.json: `Enabled` (default false), `StandingsMinutes`, `MaxRaceMinutes`, `RewardPyreals`, `RewardCap` (hard cap), `Size`. State is in memory only; adds no world objects; no database access.

Verified against ACE source: `Creature.OnDeath(DamageHistoryInfo, DamageType, bool)`, `DamageHistoryInfo.TryGetAttacker()`, `PlayerManager.BroadcastToAll`, `GetOnlinePlayer(uint)`, `WorldObjectFactory.CreateNewWorldObject(uint)` (ACE.Server.Factories), `WeenieClassName.W_COINSTACK_CLASS`, `WorldObject.SetStackSize(int?)`, `Player.TryCreateInInventoryWithNetworking(WorldObject)`, `ActionChain(IActor, Action).EnqueueChain()`. `killrace`/`racetop` are not built-in commands.

Risks: credits the last damager; a restart ends the race with no payout; a winner who is offline at the end gets no reward. Test: start a 1 minute race, kill something, `/racetop`.
