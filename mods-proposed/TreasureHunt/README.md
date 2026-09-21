# TreasureHunt (proposed, not deployed)
Riddle-and-location hunt, no world objects. Off by default (`Enabled=false`).
Commands: `/hunt start [index] | stop | status` (Admin), `/hint` (Player: very far/far/warm/hot only).
Settings.json: Hunts[{Riddle, Cell(hex), X, Y, Z, Radius, RewardPyreals}], MaxMinutes, CheckSeconds, MaxRewardPyreals (hard cap).
First online player in the same cell within Radius wins; reward is a pyreal stack (wcid 273) into their pack, delivered via ActionChain(player). Never touches ace_auth/ace_shard.
Verified in ACE source: PlayerManager.GetAllOnline/BroadcastToAll, Position.DistanceTo/Landblock/Cell, ActionChain(IActor, Action), WorldObjectFactory.CreateNewWorldObject(uint) (ACE.Server.Factories), Player.TryCreateInInventoryWithNetworking. Not verified: WorldObject.SetStackSize(int) signature, Position 8-arg constructor. No `hunt`/`hint` built-ins found.
