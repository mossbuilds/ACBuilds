#!/usr/bin/env bash
# Installed by bootstrap.sh as /usr/local/sbin/acbuilds-install-docker (root:root, 755, NOT writable by the deploy user).
# The 'acbuilds' deploy user may run exactly this file with sudo (see /etc/sudoers.d/acbuilds). deploy.sh calls it on EVERY deploy:
#   - Docker missing          -> install it (get.docker.com)
#   - a newer Docker in apt   -> upgrade only the Docker packages
#   - otherwise               -> do nothing
# Idempotent. (An upgrade restarts the Docker daemon; the containers have restart: unless-stopped and come straight back.)
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
PKGS="docker-ce docker-ce-cli containerd.io docker-compose-plugin docker-buildx-plugin"
if ! command -v docker >/dev/null 2>&1; then
  echo "== installing Docker (get.docker.com)"
  curl -fsSL https://get.docker.com | sh
else
  apt-get update -qq
  UP=$(apt list --upgradable 2>/dev/null | grep -E '^(docker-ce|docker-ce-cli|containerd\.io|docker-compose-plugin|docker-buildx-plugin)/' || true)
  if [ -n "$UP" ]; then
    echo "== upgrading Docker:"; echo "$UP"
    apt-get install -y --only-upgrade $PKGS
  else
    echo "== Docker is up to date"
  fi
fi
systemctl enable --now docker
usermod -aG docker acbuilds
docker --version
