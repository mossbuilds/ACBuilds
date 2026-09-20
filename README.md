# ACBuilds

Self-contained [ACE](https://github.com/ACEmulator/ACE) (Asheron's Call Emulator) image: .NET server + MariaDB with `ace_auth`, `ace_shard`, `ace_world` pre-seeded at build time. Built and published to GHCR automatically; a scheduled workflow (every 30 min) rebuilds whenever `ACEmulator/ACE`, the world-data release, or a watched tool repo changes.

## Quick start (auto-installs Docker if missing)

Put your AC client DAT files (`client_cell_1.dat`, `client_portal.dat`, `client_highres.dat`, `client_local_English.dat`) in a `dats/` folder next to the script, then:

| OS | Command |
|---|---|
| Windows | double-click `run.cmd` (or in a terminal: `.\run.cmd`, or `.\run.cmd -Dats D:\ac\dats`) |
| macOS | `chmod +x run.sh && ./run.sh` |
| Linux | `chmod +x run.sh && ./run.sh` |

The script checks for Docker, installs it if needed, pulls the image, starts it, and prints the **IP and port (9000)** to enter in your AC client. On Windows a fresh Docker install may need a reboot, then run the script again.

## Installing Docker by hand

- **Windows 10/11:** `winget install -e --id Docker.DockerDesktop`, reboot, start Docker Desktop (uses WSL2; enable virtualization in BIOS if asked).
- **macOS:** `brew install --cask docker` (or download Docker Desktop from docker.com), then open Docker once.
- **Linux:** `curl -fsSL https://get.docker.com | sh && sudo systemctl enable --now docker && sudo usermod -aG docker $USER` (log out/in).

> Full build order, repos pulled in and a diagram: [docs/WORKFLOW.md](docs/WORKFLOW.md)

## Two containers

| Container | Image | Holds |
|---|---|---|
| `ace-server` | `ghcr.io/mossbuilds/acbuilds-server` | the compiled ACE server (UDP 9000/9001) |
| `ace-db` | `ghcr.io/mossbuilds/acbuilds-db` | MariaDB, pre-seeded (`ace_auth`, `ace_shard`, `ace_world`); not exposed outside the compose network |

Run manually: `DATS_DIR=/path/to/dats docker compose up -d` (needs your AC client DAT files; optional `mods/` and `content/` folders are mounted too).

## Backup, restore and updates (`acb.sh` / `acb.ps1`)

```
./acb.sh backup            # accounts + characters (ace_auth, ace_shard) -> backups/ace-<time>.sql.gz   (--all adds ace_world)
./acb.sh restore backups/ace-20260919-120000.sql.gz
./acb.sh update server     # backup first, then pull + restart ONLY the server; database untouched
./acb.sh update db         # backup first, new pre-seeded DB image, then your accounts/characters restored into it
./acb.sh status
```

Windows: same commands with `.\acb.ps1` (`-All` instead of `--all`). Every `update` takes a backup first and stops if the backup fails; the backup file is kept. Copy `backups/` somewhere off the machine for real safety.

## Versions

Every build is released as `<ACE version>-acb.<n>` (e.g. `v1.78.4816-acb.2`) on the Releases page; both images carry that tag. Pin one with `ACB_TAG=v1.78.4816-acb.2 docker compose up -d`.

## Notes
- Upstream ACE targets .NET 10, so the server image uses the .NET 10 runtime.
- Drop a `.sql` in `world-override/` before building the DB image to replace the downloaded world data.
- The DB credential in `config/Config.js` is fixed; it is only reachable from the server container.

## Included mods and content
Built into the server image: [CustomClothingBase](https://github.com/OptimShi/CustomClothingBase) and the ACE.Web mod from [ACE.Mods.WebAPI](https://github.com/ACEmulator/ACE.Mods.WebAPI). Loaded into the world database: [ACEUniqueWeenies](https://github.com/titaniumweiner/ACEUniqueWeenies). Mods are built from source; one that fails to build is skipped (look for `MOD SKIPPED` in the build log).

**Windows "is not digitally signed" error:** files from a downloaded ZIP are blocked by PowerShell. Use `run.cmd` (and `acb.cmd` for backup/restore/update), which bypass that for one run, or unblock once with `Get-ChildItem -Recurse | Unblock-File` in the extracted folder.

## Self-contained launcher (no Python needed)

Every release has a single-file program with a **window**: pick your `acclient.exe`, type an account and password, click **Install & Play**. It installs Docker if missing, downloads and starts the server and database containers, waits for the world to open, then starts Asheron's Call. It also has Backup, Update, Stop buttons, plus **Install server only / database only** and **Uninstall server / database (+backup) / all** (uninstall saves a backup first and never touches your AC client or DAT files; CLI: `acbuilds uninstall`). (Windows: Docker Desktop may ask for a restart; reopen the launcher afterwards and click again.)

| OS | Download from the release |
|---|---|
| Windows | `acbuilds-windows-x64.exe` (double-click) |
| Linux | `acbuilds-linux-x64` (then `chmod +x`) |
| macOS (Apple silicon) | `acbuilds-macos-arm64` (then `chmod +x`) |

Terminal use (any arguments skip the window):

```
acbuilds up [--dats DIR] [--client PATH --account NAME --password PW]   # install, start, launch the game
acbuilds play            # just start the game against the running server
acbuilds backup [--all]
acbuilds restore FILE.sql.gz
acbuilds update [server|db|all]
acbuilds status | logs | down | version
```

Source: `launcher/acblauncher.py` (Python standard library only; run it with `python launcher/acblauncher.py` on any system).
