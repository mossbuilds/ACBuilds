#!/usr/bin/env bash
# ACBuilds admin tool (Linux/macOS): backup, restore, update, status.
#   ./acb.sh backup [--all]        dump ace_auth+ace_shard (accounts, characters) to backups/  (--all adds ace_world)
#   ./acb.sh restore <file.sql.gz> load a backup into the running database
#   ./acb.sh update [server|db|all] backup FIRST, pull new images, redeploy (db: fresh seeded DB, then your auth+shard restored)
#   ./acb.sh status                show containers and the newest backup
set -euo pipefail
cd "$(dirname "$0")"
D="docker"; docker info >/dev/null 2>&1 || D="sudo docker"
mkdir -p backups

backup() {
  local dbs="ace_auth ace_shard"; [ "${1:-}" = "--all" ] && dbs="ace_auth ace_shard ace_world"
  BACKUP_FILE="backups/ace-$(date +%Y%m%d-%H%M%S).sql.gz"
  $D exec ace-db mariadb-dump -uace -pace-local --single-transaction --databases $dbs | gzip > "$BACKUP_FILE"
  if ! gzip -t "$BACKUP_FILE" 2>/dev/null || [ "$(gunzip -c "$BACKUP_FILE" | wc -c)" -lt 1000 ]; then echo "Backup FAILED"; rm -f "$BACKUP_FILE"; exit 1; fi
  echo "Backup OK: $BACKUP_FILE ($(du -h "$BACKUP_FILE" | cut -f1))"
}
load() { gunzip -c "$1" | $D exec -i ace-db mariadb -uace -pace-local; }

case "${1:-}" in
  backup) backup "${2:-}" ;;
  restore)
    f="${2:?usage: acb.sh restore <file.sql.gz>}"; [ -f "$f" ] || { echo "No such file: $f"; exit 1; }
    $D compose stop ace-server; load "$f"; $D compose start ace-server; echo "Restore done." ;;
  update)
    what="${2:-all}"; backup   # always back up first; aborts here if it fails
    case "$what" in
      server) $D compose pull ace-server; $D compose up -d ace-server ;;
      db|all)
        $D compose pull; $D compose stop ace-server
        echo "Replacing the database with the new pre-seeded image, then restoring your accounts and characters..."
        $D compose rm -sf ace-db; $D volume rm acbuilds_ace-db >/dev/null
        $D compose up -d ace-db
        until [ "$($D inspect -f '{{.State.Health.Status}}' ace-db)" = healthy ]; do sleep 3; done
        load "$BACKUP_FILE"; $D compose up -d ;;
      *) echo "update [server|db|all]"; exit 1 ;;
    esac
    echo "Update complete. Backup kept at $BACKUP_FILE" ;;
  status) $D compose ps; ls -1t backups 2>/dev/null | head -1 | sed 's/^/Newest backup: /' ;;
  *) sed -n 2,7p "$0"; exit 1 ;;
esac
