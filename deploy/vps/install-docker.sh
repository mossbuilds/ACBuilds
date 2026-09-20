#!/usr/bin/env bash
# Installed by bootstrap.sh as /usr/local/sbin/acbuilds-install-docker (root:root, 755, NOT writable by the deploy user).
# The 'acbuilds' deploy user may run exactly this file with sudo (see /etc/sudoers.d/acbuilds). deploy.sh calls it when Docker is
# missing, so the pipeline can bring up a fresh VPS by itself. Idempotent: does nothing if Docker is already there.
set -euo pipefail
if ! command -v docker >/dev/null 2>&1; then
  echo "== installing Docker (get.docker.com)"
  curl -fsSL https://get.docker.com | sh
fi
systemctl enable --now docker
usermod -aG docker acbuilds
docker --version
