#!/bin/bash
set -euo pipefail
DBHOST="${DB_HOST:-ace-db}"
for i in $(seq 1 120); do (echo > /dev/tcp/$DBHOST/3306) 2>/dev/null && break; sleep 1; done
(echo > /dev/tcp/$DBHOST/3306) 2>/dev/null || { echo "Database $DBHOST:3306 not reachable"; exit 1; }
ls /ace/Dats/client_*.dat >/dev/null 2>&1 || echo "WARNING: no client_*.dat in /ace/Dats - mount your AC DAT files there or ACE.Server will exit."
cd /ace
# ACE reads its console from stdin; with no TTY it spins and floods the log. Hold stdin open on a pipe that never sends.
tail -f /dev/null | exec dotnet ACE.Server.dll
