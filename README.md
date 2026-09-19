# ACBuilds

Self-contained [ACE](https://github.com/ACEmulator/ACE) (Asheron's Call Emulator) image: .NET server + MariaDB with `ace_auth`, `ace_shard`, `ace_world` pre-seeded at build time. Built and published to GHCR automatically; a scheduled workflow (every 30 min) rebuilds whenever `ACEmulator/ACE`, the world-data release, or a watched tool repo changes.

## Quick start (auto-installs Docker if missing)

Put your AC client DAT files (`client_cell_1.dat`, `client_portal.dat`, `client_highres.dat`, `client_local_English.dat`) in a `dats/` folder next to the script, then:

| OS | Command |
|---|---|
| Windows (PowerShell, as admin) | `.\run.ps1` (or `.\run.ps1 -Dats D:\ac\dats`) |
un.ps1` (or `.
un.ps1 -Dats D:cdats`) |
| macOS | `chmod +x run.sh && ./run.sh` |
| Linux | `chmod +x run.sh && ./run.sh` |

The script checks for Docker, installs it if needed, pulls the image, starts it, and prints the **IP and port (9000)** to enter in your AC client. On Windows a fresh Docker install may need a reboot, then run the script again.

## Installing Docker by hand

- **Windows 10/11:** `winget install -e --id Docker.DockerDesktop`, reboot, start Docker Desktop (uses WSL2; enable virtualization in BIOS if asked).
- **macOS:** `brew install --cask docker` (or download Docker Desktop from docker.com), then open Docker once.
- **Linux:** `curl -fsSL https://get.docker.com | sh && sudo systemctl enable --now docker && sudo usermod -aG docker $USER` (log out/in).

## Run manually

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
