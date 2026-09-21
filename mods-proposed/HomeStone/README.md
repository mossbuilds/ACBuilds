# HomeStone (proposed, not deployed)
Players save a spot and teleport back to it. Commands are `/mark` and `/gomark` because `/home` already exists in ACE (checked in PlayerCommands/AdminCommands/DeveloperCommands).
Verified in ACE source: `Player.Teleport(Position, bool)`, `Player.PKTimerActive`, `Position(uint,float x8)`, `Position.Indoors`, `Position.Cell`.
Settings.json: `CooldownSeconds` (300), `AllowIndoors` (false), `DataFile` (homestone-spots.json, relative to server working dir).
Combat block: PKTimerActive only covers PK-type players. Risks: saved spot may become invalid after a landblock change; settings-instance lookup via OnStartSuccess (compile check pending); file written on every /mark.
Test: /mark, walk away, /gomark; repeat inside cooldown; try indoors. Enable later: copy folder to the mods dir.
