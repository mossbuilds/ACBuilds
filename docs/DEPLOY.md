# VPS deployment (ace.mossbuilds.xyz)

Public address: **`ace.mossbuilds.xyz`** - port **9000** = normal game (stock ACE), port **9100** = PC VR server (Thwargle/ACE fork).
Both servers use **one shared database** for now.

## What runs where
| Piece | Location |
|---|---|
| Compose file | `/opt/acbuilds/docker-compose.yml` (from `deploy/vps/docker-compose.yml`) |
| Database | Docker volume `acbuilds_ace-db` (MariaDB container `ace-db`, port 3306 NOT published) |
| Servers | `ace-server` UDP 9000/9001, `ace-vr-server` UDP 9100/9101 |
| AC DAT files | `/opt/acbuilds/dats` (`client_portal.dat`, `client_cell_1.dat`, `client_local_English.dat`; mounted read-only) |
| Backups | `/opt/acbuilds/backups/ace-<time>.sql.gz` (accounts + characters), the newest 14 are kept |
| Deploy script | `/opt/acbuilds/deploy.sh` |

## What happens on an update
1. GitHub builds and publishes new images (this already happens automatically, including when `ACEmulator/ACE` or `Thwargle/ACE` change).
2. The workflow's `deploy` job connects to the VPS with a dedicated SSH key whose only permission is to run `deploy.sh` (SSH forced command, `restrict`: no shell, no forwarding). If Docker is missing, `deploy.sh` installs it through a root-owned script that the deploy user may `sudo` (only that script), then continues.
3. `deploy.sh`: fetches the compose file for that commit, **backs up the database first** (verified with `gzip -t` and a size check; if the backup fails nothing is changed), pulls the new images, restarts the two servers, waits until both report "World is now open".
4. The database *image* is **not** swapped automatically (its volume persists, so a new image alone would not change the data). To apply a new world-data image: run `deploy.sh --recreate-db` on the VPS (backs up, replaces the database with the new pre-seeded one, restores accounts and characters).

## GitHub settings the deploy job needs
Repository secrets (Settings > Secrets and variables > Actions): `VPS_HOST`, `VPS_USER` (= `acbuilds`), `VPS_SSH_KEY` (private deploy key), `VPS_KNOWN_HOSTS` (the pinned SSH host key line for the VPS).
Repository variable: `VPS_DEPLOY` = `true` (the job is skipped until this is set).

## One-time VPS setup (root)
`bash deploy/vps/bootstrap.sh "<deploy public key>"` installs Docker if missing, creates the `acbuilds` user, opens only UDP 9000-9001 and 9100-9101 in ufw, installs `deploy.sh` and authorizes the deploy key. Then copy the DAT files to `/opt/acbuilds/dats` and run the first deploy.

## Safety notes
- **First account = admin.** ACE makes the first account created on an empty database an administrator. Create the owner account BEFORE the ports are opened to the internet.
- Account auto-creation stays on (that is how players get accounts); anyone on the internet can create an account.
- **Shared database:** two live world servers writing one shard database can clash if the same character is logged in on both at once. Log a character into only one server at a time; if it becomes a problem the VR server gets its own database.
- **Rollback:** restore a backup with `gunzip -c backups/<file> | docker exec -i ace-db mariadb -h127.0.0.1 -uace -pace-local`, and pin older images with `ACB_TAG=<release tag>` (see the Releases page).
- The DNS record for `ace.mossbuilds.xyz` must be **DNS only** (not proxied) so UDP reaches the VPS.
