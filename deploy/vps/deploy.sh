#!/usr/bin/env bash
# ACBuilds VPS deploy. Runs ON the VPS, as the restricted 'acbuilds' user, and is the ONLY command the GitHub deploy key may run
# (authorized_keys forced command). Order: back up the database FIRST -> pull new images -> restart servers -> health check.
#
#   deploy.sh <git-sha> [--warn N] called by CI: fetch the compose file for that commit, pull, warn players in game N
#                                  minutes ahead (default 10, 0 = skip), back up, restart
#   deploy.sh --recreate-db        manual: also replace the database with the new pre-seeded image (world data update),
#                                  then restore accounts + characters from the fresh backup
#   deploy.sh --backup             just take a backup
#   deploy.sh --restart            recovery: no image pull, no countdown, just restart the two game server containers
#                                  and re-check that the world opens (first response to a healthcheck failure)
#   deploy.sh --rollback <version> recovery: pin ACB_TAG=<version> (an existing GHCR tag, e.g. v1.78.4816-acb.3), pull and
#                                  restart on that image only - compose/config files and the database are left alone;
#                                  no countdown
#   deploy.sh --heal               run heal.sh once, immediately (does not take the deploy lock itself - heal.sh takes
#                                  it, so a normal deploy and a heal pass can never run at the same time either way)
set -euo pipefail
APP=/opt/acbuilds
KEEP=14                           # backups kept

# --heal execs heal.sh BEFORE this script takes .deploy.lock below, so the two never deadlock over the same flock.
if [ "${1:-${SSH_ORIGINAL_COMMAND:-}}" = "--heal" ]; then
  exec "$APP/heal.sh"
fi

cd "$APP"
exec 9>"$APP/.deploy.lock"; flock -n 9 || { echo "another deploy is running"; exit 1; }

# When invoked through the SSH forced command the argument arrives in SSH_ORIGINAL_COMMAND.
ARG="${1:-${SSH_ORIGINAL_COMMAND:-}}"
ROLLBACK_VERSION=""
WARN=10   # in-game warning minutes before a normal deploy restarts the servers; recovery paths never use this
case "$ARG" in
  --recreate-db|--backup|--restart) MODE="$ARG"; SHA="main"; WARN=0 ;;
  --rollback\ *) MODE="--rollback"; ROLLBACK_VERSION="${ARG#--rollback }"; SHA="main"; WARN=0
    [[ "$ROLLBACK_VERSION" =~ ^[A-Za-z0-9._-]+$ ]] || { echo "refusing: --rollback needs a plain image tag"; exit 2; } ;;
  "") MODE=""; SHA="main" ;;
  *\ --warn\ *)
    SHA="${ARG%% --warn *}"; WARN="${ARG##* --warn }"; MODE=""
    [[ "$SHA" =~ ^[0-9a-f]{40}$ ]] || { echo "refusing unexpected argument"; exit 2; }
    [[ "$WARN" =~ ^[0-9]+$ ]] && [ "$WARN" -ge 0 ] && [ "$WARN" -le 60 ] || { echo "refusing: --warn needs a whole number of minutes, 0-60"; exit 2; } ;;
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

# A normal deploy of a specific commit is refused if that commit is on the blacklist (builds that already failed
# post-deploy verification once). Fetched fresh from main's HEAD, never from the target sha itself - a bad sha can't
# have blacklisted itself yet. No changes are made before this check.
if [ "$MODE" = "" ] && [ "$SHA" != "main" ]; then
  if curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/deploy/BLACKLIST.txt" -o "$APP/.blacklist.tmp"; then
    if grep -qx "$SHA" "$APP/.blacklist.tmp" 2>/dev/null; then
      rm -f "$APP/.blacklist.tmp"
      echo "REFUSING: commit $SHA is on deploy/BLACKLIST.txt (failed a previous post-deploy verification). Nothing changed."
      exit 3
    fi
    rm -f "$APP/.blacklist.tmp"
  else
    echo "WARNING: could not fetch deploy/BLACKLIST.txt to check; proceeding anyway"
  fi
fi

DC="docker compose -f $APP/docker-compose.yml"
mkdir -p backups dats mods mods-vr content

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

# Warns players in game before a restart via the ACE console pipe (gamecast, visible to everyone online). Never
# touches a container's compose state, so it's safe to run before the backup. A failed broadcast never fails the deploy.
STATUS_URL="http://127.0.0.1:8618/api/status"
broadcast() {
  local c="$1" msg="$2"
  docker inspect -f '{{.State.Running}}' "$c" 2>/dev/null | grep -q true || return 0
  printf '%s\n' "gamecast $msg" | docker exec -i "$c" sh -c 'cat > /ace/console.in' 2>/dev/null || true
}
broadcast_all() {
  local msg="$1"
  echo "== broadcast: $msg"
  broadcast ace-server "$msg" || true
  broadcast ace-vr-server "$msg" || true
}
countdown() {
  local n="$1"
  if [ "$n" = "0" ]; then echo "== no countdown (warn=0)"; return 0; fi
  local players
  players=$(curl -fsS --max-time 5 "$STATUS_URL" 2>/dev/null | python3 -c '
import sys, json
try:
    d = json.load(sys.stdin)
    print(sum(s.get("players_online", 0) for s in d.get("servers", [])))
except Exception:
    print("UNKNOWN")
' 2>/dev/null || echo UNKNOWN)
  if [ "$players" = "0" ]; then echo "== nobody online - no countdown"; return 0; fi

  broadcast_all "Server update in $n minutes. Both servers will restart for about 3 minutes. Please find a safe spot and log out before then."
  local secs=$((n * 60)) prev=$((n * 60)) mark
  for mark in 300 120 60 30; do
    if [ "$mark" -lt "$secs" ]; then
      sleep $((prev - mark))
      case "$mark" in
        300) broadcast_all "Server update in 5 minutes - please get to a safe spot and log out." ;;
        120) broadcast_all "Server update in 2 minutes - please get to a safe spot and log out." ;;
        60)  broadcast_all "Server update in 1 minute - please get to a safe spot and log out." ;;
        30)  broadcast_all "Server restarting in 30 seconds - please log out now." ;;
      esac
      prev="$mark"
    fi
  done
  sleep "$prev"
  broadcast_all "Server restarting now for an update. Back in a few minutes."
}

wait_open() {
  for _ in $(seq 1 60); do docker logs "$1" 2>&1 | grep -q "World is now open" && { echo "== $1: world open"; return 0; }; sleep 5; done
  echo "ERROR: $1 did not open its world in time"; docker logs --tail 40 "$1" 2>&1 | tail -20; return 1
}

# Pins (or unpins) the image tag docker-compose.yml's ${ACB_TAG:-latest} resolves to, via the .env file compose reads
# from its own directory automatically. Set by --rollback; a normal deploy resets it to latest.
set_tag() {
  local v="$1"
  touch .env
  if grep -q '^ACB_TAG=' .env; then sed -i "s/^ACB_TAG=.*/ACB_TAG=$v/" .env; else echo "ACB_TAG=$v" >> .env; fi
}

# --restart and --rollback are recovery paths: they never fetch a new compose/config/status page from a commit (there is
# no new commit involved), and never touch the database beyond --rollback's own backup.
if [ "$MODE" = "--restart" ]; then
  echo "== restart only: no image pull, no database change"
  $DC restart ace-server ace-vr-server
  wait_open ace-server
  wait_open ace-vr-server
  echo "== restart complete ($(date -u +%FT%TZ))"
  exit 0
fi
if [ "$MODE" = "--rollback" ]; then
  echo "== rolling back to image tag $ROLLBACK_VERSION (compose/config files and the database are left alone)"
  backup
  set_tag "$ROLLBACK_VERSION"
  echo "== pulling images"
  $DC pull
  $DC up -d --remove-orphans
  wait_open ace-server
  wait_open ace-vr-server
  docker image prune -f >/dev/null
  echo "== rollback complete ($(date -u +%FT%TZ)) version=$ROLLBACK_VERSION"
  exit 0
fi

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

echo "== self-heal script (deploy/vps/heal.sh, heal_lib.py)"
for f in heal.sh heal_lib.py; do
  curl -fsSL "https://raw.githubusercontent.com/mossbuilds/ACBuilds/$SHA/deploy/vps/$f" -o "$f.new" && mv "$f.new" "$f" || { rm -f "$f.new"; echo "WARNING: could not update $f"; }
done
chmod 755 heal.sh; chmod 644 heal_lib.py 2>/dev/null || true

ls dats/client_portal.dat dats/client_cell_1.dat dats/client_local_English.dat >/dev/null 2>&1 \
  || { echo "ERROR: AC DAT files missing in $APP/dats (client_portal.dat, client_cell_1.dat, client_local_English.dat)"; exit 1; }

if [ "$MODE" = "--backup" ]; then
  backup
  exit 0
fi

# A normal deploy always goes back to :latest - otherwise a pin left by an earlier --rollback would keep every later
# deploy on the old images forever.
set_tag latest

# Pulled BEFORE the countdown, so players keep playing while the (large) images download; the countdown and backup
# below never touch a container, so nothing changes for players until after both are done.
echo "== pulling images"
$DC pull

countdown "$WARN"

backup

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
