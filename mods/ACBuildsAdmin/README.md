# ACBuildsAdmin (ACE mod)

Console-only admin commands for the ACBuilds servers. Both use ACE's `ConsoleInvoke` flag, so a **player can never run them in game**: only someone
with access to the server console (root or docker group on the host) can.

| Command | What it does |
|---|---|
| `whereis [player name]` | live position of one online player (cell, x/y/z, LOC string), or of everyone online |
| `spawnnear <wcid or classname> <player name>` | creates the object next to that online player, exactly like `/create` (creatures 5 units in front, facing the player; other objects at their use radius). Temporary, like `/create`: gone when the landblock reloads |

Run from the host (output goes to `docker logs`):

    docker exec ace-vr-server sh -c "echo 'whereis Mmm' > /ace/console.in"
    docker exec ace-vr-server sh -c "echo 'spawnnear 900021224 Mmm' > /ace/console.in"

or with the ac-creator skill: `ac_apply.py whereis vps Mmm`, `ac_apply.py spawn vps 900021224 Mmm`.

## How it is built and shipped
- Source is this folder. `Dockerfile.server` (mods stage) compiles it against the ACE binaries in the image with `server/build-mod.sh`
  (rewrites the `C:\ACE\Server` hint paths to `/ace/*.dll`); a mod that fails to build is skipped with `MOD SKIPPED` in the build log, never breaking the image.
- The image carries it in `/opt/mods/ACBuildsAdmin`; `server/entrypoint.sh` copies it into `/ace/Mods/ACBuildsAdmin` on **every** start (this mod is refreshed, unlike other mods).
- Hot load without a restart: copy the built folder into the server's Mods folder and run the console command `mod find`.
- Built and tested against ACE 1.78.x (stock build fb28703, VR fork 95ef426) on .NET 10. Uses `PlayerManager.GetOnlinePlayer/GetAllOnline`,
  `WorldObjectFactory.CreateNewWorldObject`, `Position.InFrontOf`, `WorldObject.EnterWorld`, and queues creation on the player's own landblock thread (`ActionChain`).
- Player lookup accepts the plain name (`Mmm`, not `+Mmm`) and names with spaces.

## Ideas not built yet
`createinstnear` (persistent instance at the player's position: needs the guid + `landblock_instance` write ACE's `/createinst` does), `teleplayer`, `tellplayer`, a JSON `whereis` for the status page.
