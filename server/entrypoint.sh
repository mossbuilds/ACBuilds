#!/bin/bash
set -euo pipefail
DBHOST="${DB_HOST:-ace-db}"
for i in $(seq 1 120); do (echo > /dev/tcp/$DBHOST/3306) 2>/dev/null && break; sleep 1; done
(echo > /dev/tcp/$DBHOST/3306) 2>/dev/null || { echo "Database $DBHOST:3306 not reachable"; exit 1; }
ls /ace/Dats/client_*.dat >/dev/null 2>&1 || echo "WARNING: no client_*.dat in /ace/Dats - mount your AC DAT files there or ACE.Server will exit."
# Seed baked-in mods into the (possibly user-mounted) Mods folder; existing mods and their settings are never overwritten.
mkdir -p /ace/Mods && cp -rn /opt/mods/. /ace/Mods/ 2>/dev/null || true
cd /ace

# ACE reads its admin console from stdin. With no TTY it would spin and flood the log, so stdin is fed from a named pipe that
# never reaches end-of-file. Anything written to /ace/console.in is executed as an ACE console command, and the output appears
# in `docker logs`. Example (from the host):
#   docker exec ace-server sh -c 'echo "clearcache" > /ace/console.in'
# Only someone who can already run `docker exec` on this machine (root / docker group) can use it; it is not exposed to the network.
rm -f /ace/console.in && mkfifo /ace/console.in && chmod 600 /ace/console.in
dotnet ACE.Server.dll < <(while true; do cat /ace/console.in; done) &
ACE=$!
trap 'kill -TERM "$ACE" 2>/dev/null || true' TERM INT
# the container lives and dies with ACE.Server (so `restart: unless-stopped` still works when it crashes)
code=0
wait "$ACE" || code=$?
if [ "$code" -gt 128 ]; then wait "$ACE" 2>/dev/null || code=$?; fi   # a forwarded TERM interrupts the first wait
exit "$code"
