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
DC="docker compose -f $APP/docker-compose.yml"
mkdir -p backups dats mods mods-vr content

echo "== compose file from commit $SHA"
curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/deploy/vps/docker-compose.yml" -o docker-compose.yml.new
docker compose -f docker-compose.yml.new config -q && mv docker-compose.yml.new docker-compose.yml

ls dats/client_portal.dat dats/client_cell_1.dat dats/client_local_English.dat >/dev/null 2>&1 \
  || { echo "ERROR: AC DAT files missing in $APP/dats (client_portal.dat, client_cell_1.dat, client_local_English.dat)"; exit 1; }

backup() {
  if ! docker inspect -f '{{.State.Running}}' ace-db 2>/dev/null | grep -q true; then echo "== no running database yet: nothing to back up (first deploy)"; return 0; fi
  local f="backups/ace-$(date +%Y%m%d-%H%M%S).sql.gz"
  docker exec ace-db mariadb-dump -h127.0.0.1 -uace -pace-local --single-transaction --databases ace_auth ace_shard | gzip > "$f"
  if ! gzip -t "$f" || [ "$(gunzip -c "$f" | wc -c)" -lt 1000 ]; then rm -f "$f"; echo "BACKUP FAILED - deploy aborted, nothing changed"; exit 1; fi
  echo "== backup ok: $f ($(du -h "$f" | cut -f1))"; LAST_BACKUP="$f"
  ls -1t backups/ace-*.sql.gz 2>/dev/null | tail -n +$((KEEP + 1)) | xargs -r rm -f
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
  gunzip -c "$LAST_BACKUP" | docker exec -i ace-db mariadb -h127.0.0.1 -uace -pace-local
fi
echo "== restarting"
$DC up -d --remove-orphans
until [ "$(docker inspect -f '{{.State.Health.Status}}' ace-db)" = healthy ]; do sleep 3; done
wait_open ace-server
wait_open ace-vr-server
docker image prune -f >/dev/null
echo "== deploy complete ($(date -u +%FT%TZ)) commit=$SHA"
