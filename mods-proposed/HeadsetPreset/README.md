# HeadsetPreset (proposed, not deployed, not compiled)
Admin `/headset` prints the current values of load-related server properties and how to change them. It changes NOTHING. Off by default (Enabled=false). headset/lite/lowmem/light are not in %TEMP%\cmds.txt.
Read from ACE master: PropertyManager.GetLong/GetDouble (ACE.Server.Managers), properties `teleport_visibility_fix` and `mob_awareness_range`.
Could NOT be done: ACE has no per-player visibility radius or known-object cap. ObjectMaint (ACE.Server.Physics.Common) works from cell/landblock visibility with no radius or cap field, and PropertyManager has no visibility/object-count property. Removing known objects via ObjMaint would desync KnownPlayers and could hide players/NPCs (exploit risk), so there is no player `/lite`. The client alone decides its rendering; a server mod cannot lower Quest memory use.
UNVERIFIED: the /modifylong and /modifydouble names (not re-checked against cmds.txt); the mod is not compiled.
