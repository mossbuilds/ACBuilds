# StuckRescue (proposed, not deployed, not compiled)
/unstick (Player) frees a player stuck in geometry; /unstickplayer <name> (Admin) does it for an online player. Off by default (Enabled=false). (unstick/stuck not in %TEMP%\cmds.txt;)
Outdoors: rings of 8 points every StepDistance up to MaxDistance (30) around the player, first outdoor point with GetTerrainZ+ZOffset that IsWalkable wins. Indoors, or nothing found: teleport to Player.Sanctuary (same as /lifestone), if FallbackToSanctuary.
Limits: refused if PKTimerActive or not NonCombat, LastTeleportTime < 10 s, cooldown 300 s, optional MaxPerHour.
Verified in ACE source: PositionExtensions (ACE.Server.Entity) GetCell/GetTerrainZ/IsWalkable, WorldManager.ThreadSafeTeleport, Player.LastTeleportTime, Sanctuary (used by HandleActionTeleToLifestone).
UNVERIFIED: Player.Sanctuary accessibility from a mod, Position copy constructor, exact using set (no compile). Sanctuary recall skips lifestone animation/mana cost.
