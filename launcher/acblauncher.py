#!/usr/bin/env python3
"""ACBuilds launcher: one self-contained program (Windows / macOS / Linux) that installs Docker if needed,
starts the ACE server + database containers, and prints the IP:port to connect to. Also backup / restore / update.

  acbuilds            open the GUI (Install & Play); in a terminal without a display: same as `up`
  acbuilds up [--dats DIR]
  acbuilds down | status | logs
  acbuilds backup [--all]           accounts + characters (ace_auth, ace_shard); --all adds ace_world
  acbuilds restore FILE.sql.gz
  acbuilds update [server|db|all]   backup FIRST, pull new images, redeploy
  acbuilds install [server|db|all]
  acbuilds uninstall [server|db|all] [--yes] [--purge]   never touches the AC client; db removal backs up first
  acbuilds version

Stdlib only. Files live in ./acbuilds-data next to where you run it (compose file, backups/, dats/, mods/, content/).
"""
import argparse, datetime, getpass, gzip, json, os, platform, shutil, socket, subprocess, sys, time
from pathlib import Path

try:
    from _version import VERSION  # written by CI so the exe knows its release tag
except Exception:
    VERSION = "dev"

REGISTRY = "ghcr.io/mossbuilds"
DB_PASS = ("ace", "ace-local")  # internal-only credential, see config/Config.js

COMPOSE = f"""name: acbuilds
services:
  ace-db:
    image: {REGISTRY}/acbuilds-db:${{ACB_TAG:-latest}}
    container_name: ace-db
    restart: unless-stopped
    environment:
      INNODB_BUFFER_POOL_SIZE: ${{INNODB_BUFFER_POOL_SIZE:-2G}}
    volumes:
      - ace-db:/var/lib/mysql
  ace-server:
    image: {REGISTRY}/acbuilds-server:${{ACB_TAG:-latest}}
    container_name: ace-server
    restart: unless-stopped
    depends_on:
      ace-db: {{ condition: service_healthy }}
    ports:
      - "9000:9000/udp"
      - "9001:9001/udp"
    volumes:
      - ${{DATS_DIR:-./dats}}:/ace/Dats
      - ./mods:/ace/Mods
      - ./content:/ace/Content
volumes:
  ace-db:
"""

# next to the exe when frozen (double-click safe), else the current folder
DATA = (Path(sys.executable).parent if getattr(sys, "frozen", False) else Path.cwd()) / "acbuilds-data"
IS_WIN, IS_MAC = platform.system() == "Windows", platform.system() == "Darwin"


def run(cmd, check=True, capture=False, stdin=None, env=None):
    flags = 0x08000000 if IS_WIN else 0  # CREATE_NO_WINDOW: no console flashes when running from the GUI
    if capture or stdin:
        return subprocess.run(cmd, check=check, text=not stdin, capture_output=capture, stdin=stdin, env=env, creationflags=flags)
    # stream the output line by line through print() (shown live in both the terminal and the GUI log)
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace",
                         env=env, creationflags=flags)
    for line in p.stdout:
        print(line, end="", flush=True)
    rc = p.wait()
    if check and rc != 0:
        raise subprocess.CalledProcessError(rc, cmd)
    return subprocess.CompletedProcess(cmd, rc)


def have(cmd):
    return shutil.which(cmd) is not None


def docker_ok():
    return have("docker") and run(["docker", "info"], check=False, capture=True).returncode == 0


def ensure_docker():
    print("[1/4] Checking Docker...")
    if not have("docker"):
        print("Docker not found - installing...")
        if IS_WIN:
            if not have("winget"):
                sys.exit("winget is missing. Install Docker Desktop from https://www.docker.com/products/docker-desktop/ and re-run.")
            run(["winget", "install", "-e", "--id", "Docker.DockerDesktop",
                 "--accept-source-agreements", "--accept-package-agreements"], check=False)
            print("\nDocker Desktop installed. Reboot if Windows asks, start Docker Desktop once, then run this again.")
            sys.exit(0)
        if IS_MAC:
            if not have("brew"):
                sys.exit("Homebrew is missing. Install Docker Desktop from https://www.docker.com/products/docker-desktop/ and re-run.")
            run(["brew", "install", "--cask", "docker"])
            run(["open", "-a", "Docker"], check=False)
        else:
            run(["sh", "-c", "curl -fsSL https://get.docker.com | sh"])
            run(["sudo", "systemctl", "enable", "--now", "docker"], check=False)
            run(["sudo", "usermod", "-aG", "docker", os.environ.get("USER", "")], check=False)
    if not docker_ok():
        if IS_WIN:
            exe = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Docker/Docker/Docker Desktop.exe"
            if exe.exists():
                subprocess.Popen([str(exe)])
        print("Waiting for Docker to start (accept any prompts)...")
        for _ in range(60):
            if docker_ok():
                break
            time.sleep(5)
    if not docker_ok():
        sys.exit("Docker is installed but not running (on Linux you may need to log out/in, or run with sudo).")


def compose_cmd():
    if run(["docker", "compose", "version"], check=False, capture=True).returncode == 0:
        return ["docker", "compose"]
    if have("docker-compose"):
        return ["docker-compose"]
    sys.exit("Docker Compose is missing (it ships with Docker Desktop / get.docker.com).")


def compose(*args, check=True, dats=None):
    DATA.mkdir(exist_ok=True)
    (DATA / "docker-compose.yml").write_text(COMPOSE)
    env = dict(os.environ)
    env["DATS_DIR"] = str(dats or env.get("DATS_DIR") or (DATA / "dats"))
    return run(compose_cmd() + ["-f", str(DATA / "docker-compose.yml"), "--project-directory", str(DATA), *args],
               check=check, env=env)


def lan_ip():
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("10.255.255.255", 1))  # no packet is sent; just picks the outbound interface
        ip = s.getsockname()[0]
        s.close()
        return ip
    except Exception:
        return "127.0.0.1"


CONFIG = DATA / "config.json"


def load_cfg():
    try:
        return json.loads(CONFIG.read_text())
    except Exception:
        return {}


def save_cfg(cfg):
    DATA.mkdir(exist_ok=True)
    CONFIG.write_text(json.dumps(cfg, indent=2))


def wait_open(timeout=240):
    """Block until ACE logs that the world is open, so the client does not launch into a closed server."""
    print("Waiting for the server to open the world...", end="", flush=True)
    end = time.time() + timeout
    while time.time() < end:
        r = run(["docker", "logs", "ace-server"], check=False, capture=True)
        if "World is now open" in (r.stdout or "") + (r.stderr or ""):
            print(" open.")
            return True
        print(".", end="", flush=True)
        time.sleep(4)
    print(" timed out (the server may still be starting).")
    return False


def launch_client(a, host="127.0.0.1"):
    """Ask for the acclient.exe path once (remembered), then start the game pointed at our server."""
    cfg = load_cfg()
    interactive = sys.stdin is not None and sys.stdin.isatty()
    path = getattr(a, "client", None) or cfg.get("client")
    if not path and interactive and not getattr(a, "no_client", False):
        path = input("\nPath to acclient.exe (Enter to skip launching the game): ").strip().strip('"')
    if not path:
        return
    path = str(Path(path).expanduser())
    if Path(path).is_dir():
        path = str(Path(path) / "acclient.exe")
    if not Path(path).exists():
        print(f"Client not found: {path}")
        return
    account = getattr(a, "account", None) or cfg.get("account")
    if not account and interactive:
        account = input("Account name (new names are created automatically): ").strip()
    password = getattr(a, "password", None) or (getpass.getpass("Password: ") if interactive else None)
    if not account or not password:
        print("Need an account and password to start the game (use --account / --password).")
        return
    cfg.update(client=path, account=account)  # the password is never saved
    save_cfg(cfg)
    cmd = [path, "-h", f"{host}:9000", "-a", account, "-v", password]
    if not IS_WIN and path.lower().endswith(".exe"):
        if not have("wine"):
            print("Install Wine to run acclient.exe on this OS, then run `acbuilds play`.")
            return
        cmd = ["wine"] + cmd
    print(f"Starting Asheron's Call: {Path(path).name} -> {host}:9000 as {account}")
    subprocess.Popen(cmd, cwd=str(Path(path).parent))


def cmd_play(a):
    ensure_docker()
    wait_open()
    launch_client(a)


def pull_with_retry(dats=None, tries=8, service=None):
    """Slow or flaky connections drop mid-download ('unexpected EOF'); Docker keeps finished layers, so retrying resumes."""
    for i in range(1, tries + 1):
        if compose(*(["pull", service] if service else ["pull"]), check=False, dats=dats).returncode == 0:
            return True
        print(f"Download interrupted (attempt {i}/{tries}); retrying - finished layers are kept...")
        time.sleep(5)
    return False


IMAGES = {"server": f"{REGISTRY}/acbuilds-server:latest", "db": f"{REGISTRY}/acbuilds-db:latest"}


def db_running():
    r = run(["docker", "inspect", "-f", "{{.State.Running}}", "ace-db"], check=False, capture=True)
    return r.returncode == 0 and "true" in (r.stdout or "")


def cmd_install(a):
    """Install just one part: 'server' (the ACE container) or 'db' (the pre-seeded MariaDB), or both."""
    ensure_docker()
    what = getattr(a, "what", "all")
    dats = Path(getattr(a, "dats", None) or load_cfg().get("dats") or DATA / "dats").expanduser().resolve()
    for d in ("dats", "mods", "content", "backups"):
        (DATA / d).mkdir(parents=True, exist_ok=True)
    services = {"server": ["ace-server"], "db": ["ace-db"], "all": ["ace-db", "ace-server"]}[what]
    print(f"[2/4] Downloading: {', '.join(services)}...")
    for s in services:
        if not pull_with_retry(dats, service=s):
            sys.exit(f"Could not pull {s}.")
    print("[3/4] Starting...")
    if what == "server":
        if not run(["docker", "inspect", "ace-db"], check=False, capture=True).returncode == 0:
            print("Note: no database container found. The server needs the database (install it too, or point "
                  "config/Config.js at your own MariaDB); it will wait for 'ace-db' and stop if it is missing.")
        compose("up", "-d", "--no-deps", "ace-server", dats=dats)
    elif what == "db":
        compose("up", "-d", "ace-db", dats=dats)
        wait_healthy()
    else:
        compose("up", "-d", dats=dats)
    print("[4/4] Done.")


def cmd_uninstall(a):
    """Remove the server, the database, or both from Docker. NEVER touches the AC client or any DAT files.
    Removing the database always saves a backup of accounts and characters first."""
    ensure_docker()
    what = getattr(a, "what", "all")
    label = {"server": "the ACBuilds server container and image (the database is kept)",
             "db": "the ACBuilds database container, image and data (a backup is saved first; the server will stop)",
             "all": "the ACBuilds server and database containers, their images and the database data (a backup is saved first)"}[what]
    if not getattr(a, "yes", False):
        if not (sys.stdin and sys.stdin.isatty()):
            sys.exit("Run with --yes to confirm the uninstall.")
        print(f"This removes {label}.\nYour Asheron's Call client and DAT files are NOT touched.")
        if input("Type YES to continue: ").strip() != "YES":
            sys.exit("Cancelled.")
    if what in ("db", "all"):
        if db_running():
            print("[1/3] Saving a backup of accounts and characters first...")
            do_backup()  # exits (and stops the uninstall) if the backup fails
        else:
            print("[1/3] Database is not running - no backup taken.")
    else:
        print("[1/3] No backup needed (database untouched).")
    print("[2/3] Removing...")
    if what == "all":
        compose("down", "--rmi", "all", "--volumes", "--remove-orphans", check=False)
    if what in ("server", "all"):
        compose("rm", "-sf", "ace-server", check=False)
    if what in ("db", "all"):
        compose("stop", "ace-server", check=False)  # the server cannot run without its database
        compose("rm", "-sf", "ace-db", check=False)
        run(["docker", "volume", "rm", "acbuilds_ace-db"], check=False, capture=True)
    for k in (["server", "db"] if what == "all" else [what]):
        run(["docker", "rmi", "-f", IMAGES[k]], check=False, capture=True)
    print("[3/3] Cleaning up launcher files...")
    if what == "all" and getattr(a, "purge", False) and DATA.exists():
        for child in DATA.iterdir():
            if child.name in ("dats", "backups"):
                continue  # never delete DAT files or your backups
            shutil.rmtree(child, ignore_errors=True) if child.is_dir() else child.unlink(missing_ok=True)
    print("Uninstalled. Your AC client and DAT files were not touched"
          + ("; backups are kept in " + str(DATA / "backups") if what in ("db", "all") else "")
          + ". Docker itself is left installed.")


def cmd_up(a):
    ensure_docker()
    dats_arg = getattr(a, "dats", None) or load_cfg().get("dats")
    dats = Path(dats_arg).expanduser().resolve() if dats_arg else DATA / "dats"
    dats.mkdir(parents=True, exist_ok=True)
    for d in ("mods", "content", "backups"):
        (DATA / d).mkdir(parents=True, exist_ok=True)
    if not list(dats.glob("client_*.dat")):
        sys.exit(f"Put your AC client DAT files (client_cell_1.dat, client_portal.dat, client_highres.dat, "
                 f"client_local_English.dat) in:\n  {dats}\nthen run again (or pass --dats DIR).")
    print("[2/4] Downloading the server and database images (first time is a few hundred MB)...")
    if not pull_with_retry(dats):
        sys.exit("Could not pull the images after several tries (are you online, and are the ghcr.io/mossbuilds packages public?).")
    print("[3/4] Starting the server and database...")
    compose("up", "-d", dats=dats)
    ip = lan_ip()
    print("\n" + "=" * 46)
    print("  ACE server is starting (give it ~1 minute)")
    print(f"  Connect your client to:  {ip}  port 9000")
    print("  (same machine: 127.0.0.1 port 9000)")
    print("  Client example: acclient.exe -h 127.0.0.1:9000 -a <account> -v <password>")
    print("  Any new account name auto-creates; the FIRST account becomes admin.")
    print("=" * 46)
    if not getattr(a, "no_client", False):
        print("[4/4] Waiting for the world to open, then starting Asheron's Call...")
        wait_open()
        launch_client(a)


def dexec(args, stdin=None, capture=False):
    return run(["docker", "exec", *(["-i"] if stdin else []), "ace-db", *args], check=False, stdin=stdin, capture=capture)


def do_backup(all_dbs=False):
    dbs = ["ace_auth", "ace_shard"] + (["ace_world"] if all_dbs else [])
    (DATA / "backups").mkdir(parents=True, exist_ok=True)
    f = DATA / "backups" / f"ace-{datetime.datetime.now():%Y%m%d-%H%M%S}.sql.gz"
    p = subprocess.run(["docker", "exec", "ace-db", "mariadb-dump", "-h127.0.0.1", f"-u{DB_PASS[0]}", f"-p{DB_PASS[1]}",
                        "--single-transaction", "--databases", *dbs], capture_output=True)
    if p.returncode != 0 or len(p.stdout) < 1000:
        sys.exit(f"Backup FAILED: {p.stderr.decode(errors='replace')[:300]}")
    with gzip.open(f, "wb") as g:
        g.write(p.stdout)
    print(f"Backup OK: {f} ({f.stat().st_size // 1024} KB)")
    return f


def load_sql(f):
    data = gzip.open(f, "rb").read() if str(f).endswith(".gz") else Path(f).read_bytes()
    p = subprocess.run(["docker", "exec", "-i", "ace-db", "mariadb", "-h127.0.0.1", f"-u{DB_PASS[0]}", f"-p{DB_PASS[1]}"],
                       input=data, capture_output=True)
    if p.returncode != 0:
        sys.exit(f"Restore FAILED: {p.stderr.decode(errors='replace')[:300]}")


def wait_healthy():
    for _ in range(120):
        r = run(["docker", "inspect", "-f", "{{.State.Health.Status}}", "ace-db"], check=False, capture=True)
        if r.stdout.strip() == "healthy":
            return
        time.sleep(3)
    sys.exit("Database did not become healthy.")


def cmd_restore(a):
    ensure_docker()
    f = Path(a.file)
    if not f.exists():
        sys.exit(f"No such file: {f}")
    compose("stop", "ace-server")
    load_sql(f)
    compose("start", "ace-server")
    print("Restore done.")


def cmd_update(a):
    ensure_docker()
    what = a.what
    backup = do_backup()  # always first; exits here if it fails
    if what == "server":
        if not pull_with_retry():
            sys.exit("Could not pull the server image.")
        compose("up", "-d", "ace-server")
    else:
        if not pull_with_retry():
            sys.exit("Could not pull the images.")
        compose("stop", "ace-server")
        print("Replacing the database with the new pre-seeded image, then restoring your accounts and characters...")
        compose("rm", "-sf", "ace-db")
        run(["docker", "volume", "rm", "acbuilds_ace-db"], check=False, capture=True)
        compose("up", "-d", "ace-db")
        wait_healthy()
        load_sql(backup)
        compose("up", "-d")
    print(f"Update complete. Backup kept at {backup}")


def main():
    ap = argparse.ArgumentParser(prog="acbuilds", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd")
    for name in ("up", "play"):
        s = sub.add_parser(name)
        if name == "up":
            s.add_argument("--dats")
            s.add_argument("--no-client", action="store_true", help="do not offer to start the game")
        s.add_argument("--client", help="path to acclient.exe (or its folder); remembered for next time")
        s.add_argument("--account"); s.add_argument("--password")
    sub.add_parser("down"); sub.add_parser("status"); sub.add_parser("logs"); sub.add_parser("version")
    un = sub.add_parser("uninstall", help="remove the server, the database or both (never the AC client)")
    un.add_argument("what", nargs="?", default="all", choices=["server", "db", "all"])
    un.add_argument("--yes", action="store_true"); un.add_argument("--purge", action="store_true")
    ins = sub.add_parser("install", help="install just the server, just the database, or both")
    ins.add_argument("what", nargs="?", default="all", choices=["server", "db", "all"]); ins.add_argument("--dats")
    b = sub.add_parser("backup"); b.add_argument("--all", action="store_true")
    r = sub.add_parser("restore"); r.add_argument("file")
    up = sub.add_parser("update"); up.add_argument("what", nargs="?", default="all", choices=["server", "db", "all"])
    if len(sys.argv) == 1:
        try:
            if IS_WIN or IS_MAC or os.environ.get("DISPLAY"):
                from gui import run_gui
                return run_gui()
        except Exception as e:  # no tkinter / no display: fall back to the terminal flow
            print(f"(GUI unavailable: {e})")
            if IS_WIN and getattr(sys, "frozen", False):
                import ctypes
                ctypes.windll.user32.MessageBoxW(0, f"The ACBuilds window could not start:\n{e}", "ACBuilds", 0x10)
                return
    a = ap.parse_args()
    a.cmd = a.cmd or "up"
    if a.cmd == "up" and not hasattr(a, "dats"):
        a.dats = None
    if a.cmd == "version":
        print(f"acbuilds launcher {VERSION}")
    elif a.cmd == "up":
        cmd_up(a)
    elif a.cmd == "play":
        cmd_play(a)
    elif a.cmd == "down":
        ensure_docker(); compose("down")
    elif a.cmd == "status":
        ensure_docker(); compose("ps")
    elif a.cmd == "logs":
        ensure_docker(); compose("logs", "-f", "ace-server", check=False)
    elif a.cmd == "uninstall":
        cmd_uninstall(a)
    elif a.cmd == "install":
        cmd_install(a)
    elif a.cmd == "backup":
        ensure_docker(); do_backup(a.all)
    elif a.cmd == "restore":
        cmd_restore(a)
    elif a.cmd == "update":
        cmd_update(a)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        sys.exit(130)
    except subprocess.CalledProcessError as e:
        sys.exit(f"Command failed ({e.returncode}): {' '.join(map(str, e.cmd))}")
