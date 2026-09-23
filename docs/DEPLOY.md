# VPS deployment (ace.mossbuilds.xyz)

Public address: **`ace.mossbuilds.xyz`** - port **9000** = normal game (stock ACE), port **9100** = PC VR server (Thwargle/ACE fork).
Both servers use **one MariaDB**: `ace_auth` (accounts, so one login works on both) and `ace_world` are shared, but each server has its **own shard** (`ace_shard` for stock, `ace_shard_vr` for VR: characters and items). The shard cannot be shared: each server hands out object IDs from its own counter, so a shared shard fails with `Duplicate entry ... for key PRIMARY` as soon as a character or item is created on the second server.

## What runs where
| Piece | Location |
|---|---|
| Compose file | `/opt/acbuilds/docker-compose.yml` (from `deploy/vps/docker-compose.yml`) |
| Database | Docker volume `acbuilds_ace-db` (MariaDB container `ace-db`, port 3306 NOT published) |
| Servers | `ace-server` UDP 9000/9001, `ace-vr-server` UDP 9100/9101 |
| AC DAT files | `/opt/acbuilds/dats` (`client_portal.dat`, `client_cell_1.dat`, `client_local_English.dat`; mounted read-only) |
| Backups | `/opt/acbuilds/backups/ace-<time>.sql.gz` (`ace_auth`, `ace_shard` and `ace_shard_vr`), the newest 14 are kept |
| Deploy script | `/opt/acbuilds/deploy.sh` |

## What happens on an update
1. GitHub builds and publishes new images (this already happens automatically, including when `ACEmulator/ACE` or `Thwargle/ACE` change).
2. The workflow's `deploy` job connects to the VPS with a dedicated SSH key whose only permission is to run `deploy.sh` (SSH forced command, `restrict`: no shell, no forwarding). On every deploy `deploy.sh` also runs a root-owned script (the only thing the deploy user may `sudo`) that installs Docker if it is missing and upgrades it when a newer version is available.
3. `deploy.sh`: fetches the compose file for that commit, **backs up the database first** (verified with `gzip -t` and a size check; if the backup fails nothing is changed), pulls the new images, restarts the two servers, waits until both report "World is now open".
4. The database *image* is **not** swapped automatically (its volume persists, so a new image alone would not change the data). To apply a new world-data image: run `deploy.sh --recreate-db` on the VPS (backs up, replaces the database with the new pre-seeded one, restores accounts and characters).

## GitHub settings the deploy job needs
Repository secrets (Settings > Secrets and variables > Actions): `VPS_HOST`, `VPS_USER` (= `acbuilds`), `VPS_SSH_KEY` (private deploy key), `VPS_KNOWN_HOSTS` (the pinned SSH host key line for the VPS).
`VPS_DEPLOY` = `true`, as a repository secret or variable (the deploy step is skipped until it is set).

## One-time VPS setup (root)
`bash deploy/vps/bootstrap.sh "<deploy public key>"` installs Docker if missing, creates the `acbuilds` user, opens only UDP 9000-9001 and 9100-9101 in ufw, installs `deploy.sh` and authorizes the deploy key. Then copy the DAT files to `/opt/acbuilds/dats` and run the first deploy.

## Verify, rollback and healthcheck

Every deploy is followed by an automated verification step, and the stack is polled on a schedule independently of deploys.

**Deploy-time verification** (`.github/workflows/build.yml`, `verify` job, runs after `deploy` succeeds):
1. Polls the authenticated `/api/health` endpoint (see below) up to 10 times, 30s apart (5 min total - matches `deploy.sh`'s own `wait_open` timeout), until `overall_healthy: true`.
2. **Healthy:** writes the just-deployed version to `deploy/LAST_GOOD.txt` and pushes that single-file change to `main` as a bot commit (`deploy: verified <version> [skip ci]`). `deploy/LAST_GOOD.txt` and `deploy/BLACKLIST.txt` are in `build.yml`'s push `paths-ignore`, so this commit does not retrigger a build.
3. **Unhealthy/timeout:** reads the previous good version from `deploy/LAST_GOOD.txt` and SSHes to the VPS to run `deploy.sh --rollback <previous version>`, then appends the failed commit's SHA to `deploy/BLACKLIST.txt` and pushes that (same bot-commit treatment), and fails the workflow with a summary explaining what happened.

**Blacklist:** before a normal `deploy.sh <sha>` proceeds, it fetches `deploy/BLACKLIST.txt` from `main`'s HEAD (never from the target sha itself - a bad commit hasn't blacklisted itself yet) and refuses, with no changes made, if that sha is listed. This stops a bad build from being redeployed by a retriggered workflow or a manual `workflow_dispatch`.

**Recovery modes on `deploy.sh`** (see the script's own header comment for exact usage):
- `deploy.sh --restart` - no image pull, no database change: `docker compose restart ace-server ace-vr-server`, then the existing `wait_open` check. The first, cheapest recovery step.
- `deploy.sh --rollback <version>` - pins `ACB_TAG=<version>` (any previously published GHCR tag - all versioned tags are retained indefinitely) in `/opt/acbuilds/.env`, pulls, `docker compose up -d --remove-orphans`, then `wait_open`. Compose file, `Config.js`/`config-vr.js` and the database are untouched - this only changes which image tag runs.

**Scheduled healthcheck** (`.github/workflows/healthcheck.yml`, every 30 minutes plus manual dispatch): polls `/api/health` once. If unhealthy, runs `deploy.sh --restart` on the VPS, waits ~60s, and polls again. If still unhealthy, reads `deploy/LAST_GOOD.txt` and runs `deploy.sh --rollback <version>`, then polls once more. Writes a one-line-per-step `$GITHUB_STEP_SUMMARY` for every outcome (healthy / recovered-by-restart / recovered-by-rollback / still-unhealthy). It never writes to `LAST_GOOD.txt` or `BLACKLIST.txt` - a scheduled failure isn't necessarily the currently-deployed version's fault, so only the deploy-time `verify` job updates those.

**`/api/health`** (`status/acb_status.py`, same host/port as `/api/status`, behind the same Caddy Basic Auth): `{ace_server: {running, world_open}, ace_vr_server: {running, world_open}, ace_db: {running, healthy}, cpu_percent, ram_percent, disk_percent, overall_healthy, checked_at}`. `overall_healthy` is true only when both servers are running with their world open AND the database container is running and its Docker healthcheck reports `healthy`; the resource percentages are informational and never gate `overall_healthy`.
- **Caddy:** this repo does not track the VPS's Caddyfile, so `/api/health` rides on whatever site block already proxies `/api/status` and friends to `127.0.0.1:8618` - no config change needed there. If a future Caddyfile edit ever lists routes explicitly rather than proxying the whole site, `/api/health` needs adding to that list by hand on the VPS.
- **Secret needed:** the `verify` and `healthcheck` workflows authenticate as `tom` with a new repository secret `STATUS_PASSWORD` - the same password already used for the site's Basic Auth (the vault entry backing `.\vault get site_password` / the status page's own password, whichever the VPS Caddyfile currently uses for `tom`). Add it under Settings > Secrets and variables > Actions.

## Safety notes
- **First account = admin.** ACE makes the first account created on an empty database an administrator. Create the owner account BEFORE the ports are opened to the internet.
- Account auto-creation stays on (that is how players get accounts); anyone on the internet can create an account.
- **Characters are per server:** a character made on port 9000 exists only on the stock server; make a separate one on 9100. The account (`tom`, admin) works on both.
- **Rollback:** `deploy.sh --rollback <version>` does the image-tag pin automatically now (see "Verify, rollback and healthcheck" above); to restore the database itself instead, `gunzip -c backups/<file> | docker exec -i ace-db mariadb -h127.0.0.1 -uace -pace-local`.
- The DNS record for `ace.mossbuilds.xyz` must be **DNS only** (not proxied) so UDP reaches the VPS.

## Status page (https://ace.mossbuilds.xyz/)
A small read-only service (`status/acb_status.py`, systemd unit `acb-status`, port 8618 on localhost) shows both game servers (up/starting/down, world open, uptime, memory, build), the database (accounts, characters, sizes, world content), the VPS load, and every character with its last saved position as a place name and map coordinates. Caddy serves it over HTTPS with the `tom` login (same password as the site). JSON is at `/api/status`.

- **Where players are:** `acb_status.py` converts the saved position to map coordinates and names the nearest town/landmark, or the dungeon for indoor landblocks. It uses the two Crossroads of Dereth files `locations.xml` and `cod_locations.xml`, which are **not in git** (third-party data). They live in `/opt/acbuilds/status/data/` on the VPS; put them there again on a rebuilt VPS.
- **"Online"** is inferred from the server logs (account connected and not yet disconnected, plus its most recently used character); positions are what the server last saved.
- `deploy.sh` refreshes `acb_status.py` and `index.html` from the deployed commit and restarts the service (a narrow sudoers rule allows exactly `systemctl restart acb-status`).
- The Caddy site block for `ace.mossbuilds.xyz` is in `/etc/caddy/Caddyfile` on the VPS (backup: `Caddyfile.bak-acstatus`).

## Server console from the host
Both game servers accept ACE console commands from a named pipe, so content tools and admins can run `clearcache`, `import-sql`, `export-sql`, `serverstatus`, `world open|close` and the rest without attaching a terminal:

    docker exec ace-server sh -c 'echo "clearcache" > /ace/console.in'    # output appears in: docker logs ace-server

Only someone who can already run `docker exec` (root / docker group) can use it; it is not reachable from the network. `stop-now` ends the server and the container exits (Docker then restarts it).

## ACBuildsAdmin mod (console-only commands)
`mods/ACBuildsAdmin` is our own ACE mod, built into both server images (Dockerfile.server mods stage) and refreshed on every container start. It adds `whereis [player]`
and `spawnnear <wcid> <player>` to the server console (flag ConsoleInvoke: players cannot run them in game). Used by the Moss `ac-creator` skill to put content next to a player.
Try it: `docker exec ace-vr-server sh -c "echo 'whereis' > /ace/console.in"` then `docker logs --tail 5 ace-vr-server`.
