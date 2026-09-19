#!/bin/bash
set -euo pipefail
# First boot on an empty /var/lib/mysql volume: copy in the pre-seeded data.
if [ -z "$(ls -A /var/lib/mysql 2>/dev/null)" ]; then cp -a /opt/mysql-seed/. /var/lib/mysql/; fi
mkdir -p /run/mysqld && chown -R mysql:mysql /run/mysqld /var/lib/mysql
exec mariadbd --user=mysql --innodb-buffer-pool-size="${INNODB_BUFFER_POOL_SIZE:-2G}"
