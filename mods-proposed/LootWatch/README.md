# LootWatch (proposed, not deployed, not compiled)

Read-only audit substitute for a corpse loot-permission blocker. ACE's own `Corpse.HasPermission(Player player)`
(verified, ACE.Server.WorldObjects) already enforces killer/fellowship/half-life loot rights correctly - this mod
never overrides or weakens that check, it only records outcomes for later dispute review ("someone looted my kill").

Logs to `lootwatch.log` (server working dir) one line per monster-corpse open by a player who is neither the recorded
`KillerId` nor sharing loot via that killer's online fellowship: UTC time, opener's CHARACTER name, the corpse's `Name`
(already "Corpse of `<creature name>`", set in `Creature_Death.cs`), the killer id if any, whether the corpse was
already `IsLooted`, landblock hex. No account names, IPs or ace_auth/ace_shard access. Rotates at `MaxKb`, keeps
`MaxFiles` (`.1`..`.N`), same rotation as TradeLedger.

Patch: Harmony postfix on public `Corpse.Open(Player player)` (ACE.Server.WorldObjects, Corpse.cs). `Open()` calls
`HasPermission(player)` first and returns early, without calling `Container.Open`, when permission is denied - so
`IsOpen`/`Viewer` are only set on an actual, successful open. The postfix checks `IsOpen && Viewer == player.Guid.Full`
to confirm the open really happened, rather than re-calling `HasPermission` (which has side effects: it mutates the
`permitteeOpened` set and removes entries from `player.LootPermission` on a passing call - calling it twice would
double those effects). The killer's-fellowship check is a plain read of `player.Fellowship` against the online
killer's `Fellowship` (`PlayerManager.GetOnlinePlayer`), the same comparison `HasPermission`'s own loot-share branch
makes, never a second call into `HasPermission` itself. Only monster corpses are logged (`Corpse.IsMonster`); player
corpses are out of scope for this idea. Never blocks or changes a loot action - that is ACE's own `HasPermission`'s
job. Wrapped in try/catch so a logging failure can never throw into ACE's loot path.

Commands (Sentinel+): `/lootlog [n]` (max 50), `/lootlog <character name>`. Settings: Enabled (false), LogFile, MaxKb,
MaxFiles.

Verified this round: `Container.IsOpen` (`WorldObject_Properties.cs`, backed by `PropertyBool.Open`) and
`Container.Viewer` (`Container_Properties.cs`, backed by `PropertyInstanceId.Viewer`) are both public
get/set properties on `ACE.Server.WorldObjects.Container`, readable from a Harmony postfix in another assembly.

Unverified: compile; whether `/lootlog` collides with any other proposed-but-uncompiled mod's command name (checked
against `%TEMP%\cmds.txt` and the other mods-proposed folders this round - none found, but that list is not
exhaustive of any future mod).
