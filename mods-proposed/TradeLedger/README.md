# TradeLedger (proposed, not deployed, not compiled)

Logs each completed secure player-to-player trade to `tradeledger.log` (server working dir): UTC time, both CHARACTER names, items given each side with stack sizes, pyreals each side, landblock hex. No account names, IPs or session data. Rotates at `MaxKb`, keeps `MaxFiles` (`.1`..`.N`).

Patch: Harmony prefix+postfix on private `Player.FinalizeTrade(Player target)` (ACE.Server.WorldObjects, Player_Trade.cs). Prefix snapshots both trade windows (items are removed inside the method); postfix logs only if `TradeTransferInProgress` is set (true only after verification passed). Read-only, try/catch, no DB access.

Commands (Sentinel+): `/tradelog [n]` (max 50), `/tradelog <character name>`. Settings: Enabled (false), LogFile, MaxKb, MaxFiles, LogItems, LogCoins.

Unverified: compile; `ref` tuple? __state type in a Harmony prefix; WeenieType.Coin identifies pyreals; the postfix flag assumption (flag reset happens 0.5 s later in an action chain, so the postfix sees it set).
