#!/usr/bin/env bash
# One-time VPS setup for ACBuilds. Run as root:   bash bootstrap.sh "<deploy public key line>"
# Creates a restricted 'acbuilds' user, installs Docker if missing, opens ONLY the game UDP ports, installs deploy.sh and
# authorizes the GitHub deploy key so that it can run nothing except deploy.sh (SSH forced command, no shell, no forwarding).
set -euo pipefail
PUB="${1:?usage: bootstrap.sh \"ssh-ed25519 AAAA... comment\"}"
APP=/opt/acbuilds
RAW=https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/deploy/vps

command -v docker >/dev/null 2>&1 || { echo "== installing Docker"; curl -fsSL https://get.docker.com | sh; }
systemctl enable --now docker

id acbuilds >/dev/null 2>&1 || useradd -m -s /bin/bash acbuilds
usermod -aG docker acbuilds
install -d -o acbuilds -g acbuilds "$APP" "$APP/dats" "$APP/mods" "$APP/mods-vr" "$APP/content" "$APP/backups"
curl -fsSL "$RAW/deploy.sh" -o "$APP/deploy.sh"
curl -fsSL "$RAW/docker-compose.yml" -o "$APP/docker-compose.yml"
chown acbuilds:acbuilds "$APP/deploy.sh" "$APP/docker-compose.yml"; chmod 755 "$APP/deploy.sh"

# the deploy key can run deploy.sh and nothing else
install -d -m 700 -o acbuilds -g acbuilds /home/acbuilds/.ssh
echo "restrict,command=\"$APP/deploy.sh\" $PUB" > /home/acbuilds/.ssh/authorized_keys
chown acbuilds:acbuilds /home/acbuilds/.ssh/authorized_keys; chmod 600 /home/acbuilds/.ssh/authorized_keys

# firewall: game ports only (UDP). Existing rules (ssh, web) are left alone.
if command -v ufw >/dev/null 2>&1; then
  ufw allow 9000:9001/udp comment 'ACBuilds stock ACE'
  ufw allow 9100:9101/udp comment 'ACBuilds VR ACE'
fi
echo "== bootstrap done. Next: copy the AC DAT files (client_portal.dat, client_cell_1.dat, client_local_English.dat) to $APP/dats"
