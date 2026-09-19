#!/usr/bin/env bash
# ACBuilds one-shot launcher for Linux and macOS: installs Docker if missing, pulls + runs the image, prints where to connect.
# Usage: ./run.sh [/path/to/dats]   (folder with client_cell_1.dat, client_portal.dat, client_highres.dat, client_local_English.dat)
set -euo pipefail
IMAGE="${IMAGE:-ghcr.io/mossbuilds/acbuilds:latest}"
DATS="${1:-$PWD/dats}"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker not found - installing..."
  if [ "$(uname)" = "Darwin" ]; then
    command -v brew >/dev/null || { echo "Install Homebrew first: https://brew.sh"; exit 1; }
    brew install --cask docker
    open -a Docker
  else
    curl -fsSL https://get.docker.com | sh
    sudo systemctl enable --now docker
    sudo usermod -aG docker "$USER" || true
  fi
fi
DOCKER="docker"
if ! docker info >/dev/null 2>&1; then
  if [ "$(uname)" = "Darwin" ]; then echo "Waiting for Docker Desktop to start (accept any prompts)..."; for i in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 5; done
  else DOCKER="sudo docker"; fi
fi

mkdir -p "$DATS"
ls "$DATS"/client_*.dat >/dev/null 2>&1 || { echo "Put your AC client DAT files in: $DATS  then re-run."; exit 1; }

$DOCKER pull "$IMAGE"
$DOCKER rm -f ace >/dev/null 2>&1 || true
$DOCKER run -d --name ace --restart unless-stopped -p 9000:9000/udp -p 9001:9001/udp \
  -v "$DATS":/ace/Dats -v ace-db:/var/lib/mysql "$IMAGE" >/dev/null

if [ "$(uname)" = "Darwin" ]; then IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo 127.0.0.1)
else IP=$(hostname -I 2>/dev/null | awk '{print $1}'); IP=${IP:-127.0.0.1}; fi
echo
echo "=============================================="
echo "  ACE server is starting (give it ~1 minute)"
echo "  Connect your client to:  $IP : 9000"
echo "  (same machine: 127.0.0.1 : 9000)"
echo "  Logs: docker logs -f ace"
echo "=============================================="
