#!/bin/bash
set -euo pipefail
# First boot on an empty /var/lib/mysql volume: copy in the pre-seeded data.
if [ -z "$(ls -A /var/lib/mysql 2>/dev/null)" ]; then cp -a /opt/mysql-seed/. /var/lib/mysql/; fi
mkdir -p /run/mysqld && chown -R mysql:mysql /run/mysqld /var/lib/mysql
BP="${INNODB_BUFFER_POOL_SIZE:-2G}"
mariadbd --user=mysql --bind-address=127.0.0.1 --innodb-buffer-pool-size="$BP" &
DB=$!
for i in $(seq 1 120); do mariadb-admin -h127.0.0.1 -uace -pace-local ping >/dev/null 2>&1 && break; sleep 1; done
mariadb-admin -h127.0.0.1 -uace -pace-local ping >/dev/null || { echo "MariaDB failed to start"; exit 1; }
if ! ls /ace/Dats/client_*.dat >/dev/null 2>&1; then echo "WARNING: no client_*.dat files in /ace/Dats - mount your AC DAT files there or ACE.Server will exit."; fi
term() { kill -TERM "$ACE" 2>/dev/null || true; wait "$ACE" 2>/dev/null || true; kill -TERM "$DB" 2>/dev/null || true; wait "$DB" 2>/dev/null || true; exit 0; }
trap term TERM INT
cd /ace
dotnet ACE.Server.dll &
ACE=$!
wait -n "$ACE" "$DB" || true
term
