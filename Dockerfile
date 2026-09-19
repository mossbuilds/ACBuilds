# syntax=docker/dockerfile:1.7
# ACE (Asheron's Call Emulator) - single self-contained image: .NET server + MariaDB, DB pre-seeded at build time.
# NOTE: upstream ACE master targets net10.0 (not 8), so SDK/runtime are 10.0 on Ubuntu 24.04 (noble).

ARG ACE_REPO=https://github.com/ACEmulator/ACE.git
ARG ACE_REF=master
ARG WORLD_REPO=ACEmulator/ACE-World-16PY-Patches
ARG WORLD_TAG=latest

############################ build stage: compile ACE ############################
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG ACE_REPO
ARG ACE_REF
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /src
# ACE_REF is a commit SHA in CI, so a new upstream commit busts the cache exactly here.
RUN git init . && git remote add origin "$ACE_REPO" && git fetch --depth 1 origin "$ACE_REF" && git checkout FETCH_HEAD
RUN dotnet publish Source/ACE.Server/ACE.Server.csproj -c Release -o /ace

############################ data stage: DB scripts + world data ############################
FROM ubuntu:24.04 AS data
ARG WORLD_REPO
ARG WORLD_TAG
RUN apt-get update && apt-get install -y --no-install-recommends curl unzip jq ca-certificates && rm -rf /var/lib/apt/lists/*
COPY --from=build /src/Database /data/Database
WORKDIR /data/world
# Fetch the world-data zip from the release (WORLD_TAG=latest resolves the newest release).
# To inject your own world SQL instead: drop a *.sql into ./world-override/ and it replaces this download (see Dockerfile final stage).
RUN set -eux; \
    if [ "$WORLD_TAG" = "latest" ]; then api="https://api.github.com/repos/$WORLD_REPO/releases/latest"; \
    else api="https://api.github.com/repos/$WORLD_REPO/releases/tags/$WORLD_TAG"; fi; \
    url=$(curl -fsSL "$api" | jq -r '.assets[] | select(.name|endswith(".sql.zip")) | .browser_download_url' | head -n1); \
    test -n "$url"; curl -fsSL -o world.zip "$url"; unzip -q world.zip; rm world.zip; ls -la

############################ final stage ############################
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble
ARG DEBIAN_FRONTEND=noninteractive
RUN apt-get update && apt-get install -y --no-install-recommends mariadb-server mariadb-client net-tools && rm -rf /var/lib/apt/lists/*

COPY --from=build /ace /ace
COPY --from=data /data /tmp/data
COPY world-override/ /tmp/world-override/
COPY config/Config.js /ace/Config.js
COPY config/mariadb-ace.cnf /etc/mysql/mariadb.conf.d/99-ace.cnf
COPY entrypoint.sh /entrypoint.sh
COPY seed-db.sh /tmp/seed-db.sh
RUN chmod +x /entrypoint.sh /tmp/seed-db.sh && mkdir -p /ace/Dats /ace/Mods /ace/Content

# Pre-seed: start a private mariadbd (no network), create DBs + user, load base -> incremental updates -> world data,
# shut down cleanly, then stash the data dir as /opt/mysql-seed. The entrypoint copies it into /var/lib/mysql on first
# boot when the volume is empty, so users can persist /var/lib/mysql without losing the pre-seeded state.
RUN /tmp/seed-db.sh && rm -rf /tmp/data /tmp/world-override /tmp/seed-db.sh

ARG ACE_REF
ARG WORLD_TAG
LABEL org.opencontainers.image.source="https://github.com/mossbuilds/ACBuilds" \
      ace.ref="${ACE_REF}" ace.world.tag="${WORLD_TAG}"
WORKDIR /ace
EXPOSE 9000/udp 9001/udp
VOLUME ["/var/lib/mysql", "/ace/Dats", "/ace/Mods", "/ace/Content"]
ENTRYPOINT ["/entrypoint.sh"]
