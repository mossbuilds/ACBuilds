#!/usr/bin/env bash
# ACBuilds VPS deploy. Runs ON the VPS, as the restricted 'acbuilds' user, and is the ONLY command the GitHub deploy key may run
# (authorized_keys forced command). Order: back up the database FIRST -> pull new images -> restart servers -> health check.
#
#   deploy.sh <git-sha>            called by CI: fetch the compose file for that commit, back up, pull, restart
#   deploy.sh --recreate-db        manual: also replace the database with the new pre-seeded image (world data update),
#                                  then restore accounts + characters from the fresh backup
#   deploy.sh --backup             just take a backup
set -euo pipefail
APP=/opt/acbuilds
KEEP=14                           # backups kept
cd "$APP"
exec 9>"$APP/.deploy.lock"; flock -n 9 || { echo "another deploy is running"; exit 1; }

# When invoked through the SSH forced command the argument arrives in SSH_ORIGINAL_COMMAND.
ARG="${1:-${SSH_ORIGINAL_COMMAND:-}}"
case "$ARG" in
  --recreate-db|--backup) MODE="$ARG"; SHA="main" ;;
  "") MODE=""; SHA="main" ;;
  *) [[ "$ARG" =~ ^[0-9a-f]{40}$ ]] || { echo "refusing unexpected argument"; exit 2; }; MODE=""; SHA="$ARG" ;;
esac
# Docker: the pipeline installs it if missing and upgrades it when a newer version exists, through the ONE root-owned script this
# user may sudo. A fresh install re-executes under the docker group (a new group only applies to new sessions).
FRESH=0; command -v docker >/dev/null 2>&1 || FRESH=1
sudo -n /usr/local/sbin/acbuilds-install-docker
if [ "$FRESH" = 1 ]; then
  exec 9>&-   # release the lock so the re-executed copy can take it
  exec sg docker -c "$APP/deploy.sh ${ARG:-}"
fi
for _ in $(seq 1 30); do docker info >/dev/null 2>&1 && break; sleep 2; done   # an upgrade briefly restarts the daemon
docker info >/dev/null 2>&1 || { echo "ERROR: user $(whoami) cannot talk to Docker"; exit 1; }
if [ -z "${ACB_SELF_UPDATED:-}" ] && [ "$SHA" != "main" ]; then
  if curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/deploy/vps/deploy.sh" -o "$APP/deploy.sh.new" && ! cmp -s "$APP/deploy.sh.new" "$APP/deploy.sh"; then
    echo "== deploy.sh updated from commit $SHA; restarting with it"
    chmod 755 "$APP/deploy.sh.new"; mv "$APP/deploy.sh.new" "$APP/deploy.sh"
    exec 9>&-; ACB_SELF_UPDATED=1 exec "$APP/deploy.sh" "$ARG"
  fi
  rm -f "$APP/deploy.sh.new"
fi
DC="docker compose -f $APP/docker-compose.yml"
mkdir -p backups dats mods mods-vr content


echo "== compose file from commit $SHA"
curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/deploy/vps/docker-compose.yml" -o docker-compose.yml.new
docker compose -f docker-compose.yml.new config -q && mv docker-compose.yml.new docker-compose.yml

echo "== VR server config (own shard database ace_shard_vr)"
curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/config/Config.js" -o config.js.new
sed '/"Shard":/ s/"Database": *"ace_shard"/"Database": "ace_shard_vr"/' config.js.new > config-vr.js
rm -f config.js.new
grep -q '"ace_shard_vr"' config-vr.js || { echo "ERROR: could not derive the VR config"; exit 1; }

echo "== status page (https://ace.mossbuilds.xyz/)"
mkdir -p status/data
for f in acb_status.py index.html; do
  curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/status/$f" -o "status/$f.new" && mv "status/$f.new" "status/$f" || { rm -f "status/$f.new"; echo "WARNING: could not update status/$f"; }
done
sudo -n /usr/bin/systemctl restart acb-status 2>/dev/null || echo "note: acb-status service not installed or not restartable by this user"

ls dats/client_portal.dat dats/client_cell_1.dat dats/client_local_English.dat >/dev/null 2>&1 \
  || { echo "ERROR: AC DAT files missing in $APP/dats (client_portal.dat, client_cell_1.dat, client_local_English.dat)"; exit 1; }

DBR="docker exec ace-db mariadb -uroot"        # root over the container's unix socket (never exposed)
backup() {
  if ! docker inspect -f '{{.State.Running}}' ace-db 2>/dev/null | grep -q true; then echo "== no running database yet: nothing to back up (first deploy)"; return 0; fi
  local dbs; dbs=$($DBR -N -e "SELECT GROUP_CONCAT(schema_name SEPARATOR ' ') FROM information_schema.schemata WHERE schema_name IN ('ace_auth','ace_shard','ace_shard_vr')")
  local f="backups/ace-$(date +%Y%m%d-%H%M%S).sql.gz"
  docker exec ace-db mariadb-dump -uroot --single-transaction --databases $dbs | gzip > "$f"
  if ! gzip -t "$f" || [ "$(gunzip -c "$f" | wc -c)" -lt 1000 ]; then rm -f "$f"; echo "BACKUP FAILED - deploy aborted, nothing changed"; exit 1; fi
  echo "== backup ok: $f ($(du -h "$f" | cut -f1)) databases: $dbs"; LAST_BACKUP="$f"
  ls -1t backups/ace-*.sql.gz 2>/dev/null | tail -n +$((KEEP + 1)) | xargs -r rm -f
}

# The VR server has its own shard database inside the same MariaDB (accounts + world stay shared). Created once; idempotent.
ensure_vr_shard() {
  $DBR -e "CREATE DATABASE IF NOT EXISTS ace_shard_vr; GRANT ALL ON ace_shard_vr.* TO 'ace'@'%'; FLUSH PRIVILEGES;"
  local n; n=$($DBR -N -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='ace_shard_vr'")
  if [ "$n" = "0" ]; then
    echo "== creating the VR shard schema (structure copied from ace_shard)"
    docker exec ace-db mariadb-dump -uroot --no-data ace_shard | docker exec -i ace-db mariadb -uroot ace_shard_vr
  fi
}

wait_open() {
  for _ in $(seq 1 60); do docker logs "$1" 2>&1 | grep -q "World is now open" && { echo "== $1: world open"; return 0; }; sleep 5; done
  echo "ERROR: $1 did not open its world in time"; docker logs --tail 40 "$1" 2>&1 | tail -20; return 1
}

backup
[ "$MODE" = "--backup" ] && exit 0

echo "== pulling images"
$DC pull
if [ "$MODE" = "--recreate-db" ]; then
  [ -n "${LAST_BACKUP:-}" ] || { echo "no backup available, refusing to recreate the database"; exit 1; }
  echo "== replacing the database with the new pre-seeded image, then restoring accounts and characters"
  $DC stop ace-server ace-vr-server || true
  $DC rm -sf ace-db
  docker volume rm acbuilds_ace-db
  $DC up -d ace-db
  until [ "$(docker inspect -f '{{.State.Health.Status}}' ace-db)" = healthy ]; do sleep 3; done
  gunzip -c "$LAST_BACKUP" | docker exec -i ace-db mariadb -uroot
fi
echo "== restarting"
$DC up -d ace-db
until [ "$(docker inspect -f '{{.State.Health.Status}}' ace-db)" = healthy ]; do sleep 3; done
ensure_vr_shard
$DC up -d --remove-orphans
wait_open ace-server
wait_open ace-vr-server
docker image prune -f >/dev/null
echo "== deploy complete ($(date -u +%FT%TZ)) commit=$SHA"
