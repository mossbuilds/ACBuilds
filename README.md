# ACBuilds

Self-contained [ACE](https://github.com/ACEmulator/ACE) (Asheron's Call Emulator) image: .NET server + MariaDB with `ace_auth`, `ace_shard`, `ace_world` pre-seeded at build time. Built and published to GHCR automatically; a scheduled workflow (every 30 min) rebuilds whenever `ACEmulator/ACE`, the world-data release, or a watched tool repo changes.

## Run

You must supply your own AC client DAT files (not redistributable).

```
docker run -d --name ace \
  -p 9000:9000/udp -p 9001:9001/udp \
  -v /path/to/dats:/ace/Dats \
  -v ace-db:/var/lib/mysql \
  ghcr.io/mossbuilds/acbuilds:latest
```

Optional mounts: `/ace/Mods` (Harmony mods), `/ace/Content` (world customization SQL). Set `-e INNODB_BUFFER_POOL_SIZE=512M` on small hosts (default 2G). Auto account creation is on; connect your client to the host on port 9000.

## Notes
- Upstream ACE targets .NET 10, so the image uses the .NET 10 runtime.
- Drop a `.sql` in `world-override/` before building to replace the downloaded world data.
- MariaDB listens on 127.0.0.1 only, with a fixed local credential in `config/Config.js`.
