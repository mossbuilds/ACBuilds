# ACBuilds

Self-contained [ACE](https://github.com/ACEmulator/ACE) (Asheron's Call Emulator) image: .NET server + MariaDB with `ace_auth`, `ace_shard`, `ace_world` pre-seeded at build time. Built and published to GHCR automatically; a scheduled workflow (every 30 min) rebuilds whenever `ACEmulator/ACE`, the world-data release, or a watched tool repo changes.

## You need the Asheron's Call client (not included)

This project only provides the **server and database**. You install the game client and its DAT files yourself: follow the guide at **https://www.accpp.net/manual-installation**. The launcher points your DAT folder at that install (`client_cell_1.dat`, `client_portal.dat`, `client_highres.dat`, `client_local_English.dat`) and starts `acclient.exe` for you. Nothing copyrighted is ever stored in our images.

**Alternative client: [OpenAC](https://github.com/eriknihlen/OpenAC/releases/latest)** (MIT-licensed, open source, cross-platform, made for ACE servers). It ships no game data either, so you still supply your own `client_*.dat` files. It needs a Vulkan 1.3 capable GPU, and it is beta software. Point it at this server (`127.0.0.1`, port `9000`) the same way as the retail client. The ACBuilds launcher can start it for you: in the window pick **Play with: OpenAC** (it auto-detects your install and prepared data package), or on the command line use `acbuilds up --client-type openac` / `acbuilds play --client-type openac`. Switch back any time with `--client-type retail`.

## Playing in VR (AC:VR, PC VR or native Quest)

There is an existing community VR client, **AC:VR** by Thwargle: <http://thwargle.com/unreal-vr/> (PC VR through SteamVR, or native Quest 3). It is a separate download and needs your own retail DAT files.

**A second, VR-enabled server.** AC:VR's tracked hands, physical combat and VR-aimed spells need the authors' VR-enabled ACE fork, [Thwargle/ACE](https://github.com/Thwargle/ACE). ACBuilds builds it as its **own separate server**: its own images (`acbuilds-vr-server`, `acbuilds-vr-db`), its own database and volume, on **port 9100**. It never replaces or touches the stock server on port 9000, and both can run at the same time.

- Install and start it: `acbuilds install vr` (or the **Install VR server** button, or `docker compose --profile vr up -d`).
- Back up, update, remove: `acbuilds backup --vr`, `acbuilds update vr`, `acbuilds uninstall vr` (the last one saves a backup first).
- The VR server's accounts are separate from the stock server's.

**Steps**
1. Install AC:VR from the link above (Windows setup for PC VR; the Quest installer for a standalone headset).
2. Start the VR server (above).
3. In AC:VR's login screen add a custom server: host = `127.0.0.1` (same PC) or this PC's LAN IP (native Quest, over Wi-Fi), port `9100`, type **ACE**. Add an account and press Launch.

The ACBuilds launcher can do all of it: choose **AC:VR (PC VR, SteamVR)** in Step 1 (or `acbuilds play --client-type acvr`). It starts the VR server (not the stock one), finds your AC:VR install (the "AC VR (SteamVR)" shortcut or `AC-VR.bat`), checks that SteamVR is the OpenXR runtime, starts SteamVR if it is not running, starts AC:VR, and tells you the address to add. Connect your headset (Quest: Link or Air Link) before you click. AC:VR asks for the account inside its own login screen.

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
