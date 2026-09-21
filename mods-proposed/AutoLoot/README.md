# AutoLoot (proposed, not deployed)

Opt-in per player: `/autoloot` toggles (resets on server restart). While on, opening a monster corpse you have permission to loot moves coins, trade notes (and optionally gems) straight into your pack. Items that do not fit go back on the corpse.

Settings.json: `Coins`, `TradeNotes`, `Gems`, `CloseCorpse`.

Verified against ACE source: `Corpse.Open(Player)`, `Corpse.IsMonster`, `Container.TryRemoveFromInventory(ObjectGuid, out WorldObject, bool)`, `Player.TryCreateInInventoryWithNetworking`, `Container.TryAddToInventory`, `Container.Close(Player)`, `WeenieType.Coin`, `ItemType.PromissoryNote/Gem`.

Risks: the client corpse view is stale after removal, hence CloseCorpse (default on). Player corpses are never touched.
Test: enable mod on a dev server, `/autoloot`, kill a mob, open corpse. Enable later by copying to mods/ and Enabled=true in Meta.json.
