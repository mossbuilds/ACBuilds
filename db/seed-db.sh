#!/bin/bash
# Build-time DB seeding. Order per ACE wiki: base SQL -> incremental Updates (oldest->newest) -> world data.
set -euo pipefail
D=/tmp/data/Database
mkdir -p /run/mysqld && chown mysql:mysql /run/mysqld
rm -rf /var/lib/mysql && mysql_install_db --user=mysql --datadir=/var/lib/mysql >/dev/null
mariadbd --user=mysql --skip-networking --socket=/run/mysqld/mysqld.sock &
PID=$!
for i in $(seq 1 60); do mariadb-admin --socket=/run/mysqld/mysqld.sock ping >/dev/null 2>&1 && break; sleep 1; done
M="mariadb --socket=/run/mysqld/mysqld.sock"
$M -e "CREATE DATABASE ace_auth; CREATE DATABASE ace_shard; CREATE DATABASE ace_world;
       CREATE USER 'ace'@'%' IDENTIFIED BY 'ace-local'; 
       GRANT ALL ON ace_auth.* TO 'ace'@'%'; GRANT ALL ON ace_shard.* TO 'ace'@'%'; GRANT ALL ON ace_world.* TO 'ace'@'%'; FLUSH PRIVILEGES;"
# World first: Shard updates join against ace_world tables.
# World data: a *.sql in world-override/ wins over the downloaded release.
W=$(ls /tmp/world-override/*.sql 2>/dev/null | head -n1 || true)
[ -n "$W" ] || W=$(ls /tmp/data/world/*.sql | head -n1)
echo "world data: $W"; $M ace_world < "$W"
# Custom weenies (ACEUniqueWeenies): one .sql per weenie, layered over the official world data. A bad file is skipped, not fatal.
find /tmp/data/weenies/weenies -name "*.sql" -print0 | sort -z | while IFS= read -r -d "" f; do echo "weenie: $f"; $M ace_world < "$f" || echo "SKIPPED $f"; done
$M ace_auth  < $D/Base/AuthenticationBase.sql
$M ace_shard < $D/Base/ShardBase.sql
for f in $(ls $D/Updates/Authentication/*.sql 2>/dev/null | sort); do echo "auth update: $f"; $M ace_auth < "$f"; done
for f in $(ls $D/Updates/Shard/*.sql 2>/dev/null | sort); do echo "shard update: $f"; $M ace_shard < "$f"; done
mariadb-admin --socket=/run/mysqld/mysqld.sock shutdown; wait $PID || true
rm -rf /opt/mysql-seed && mkdir -p /opt && cp -a /var/lib/mysql /opt/mysql-seed && rm -rf /var/lib/mysql/*
