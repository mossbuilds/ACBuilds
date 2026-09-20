#!/usr/bin/env python3
"""ACBuilds launcher: one self-contained program (Windows / macOS / Linux) that installs Docker if needed,
starts the ACE server + database containers, and prints the IP:port to connect to. Also backup / restore / update.

  acbuilds            start everything (same as `up`)
  acbuilds up [--dats DIR]
  acbuilds down | status | logs
  acbuilds backup [--all]           accounts + characters (ace_auth, ace_shard); --all adds ace_world
  acbuilds restore FILE.sql.gz
  acbuilds update [server|db|all]   backup FIRST, pull new images, redeploy
  acbuilds version

Stdlib only. Files live in ./acbuilds-data next to where you run it (compose file, backups/, dats/, mods/, content/).
"""
import argparse, datetime, gzip, os, platform, shutil, socket, subprocess, sys, time
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

DATA = Path.cwd() / "acbuilds-data"
IS_WIN, IS_MAC = platform.system() == "Windows", platform.system() == "Darwin"


def run(cmd, check=True, capture=False, stdin=None, env=None):
    return subprocess.run(cmd, check=check, text=not stdin, capture_output=capture, stdin=stdin, env=env)


def have(cmd):
    return shutil.which(cmd) is not None


def docker_ok():
    return have("docker") and run(["docker", "info"], check=False, capture=True).returncode == 0


def ensure_docker():
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


def cmd_up(a):
    ensure_docker()
    dats = Path(a.dats).expanduser().resolve() if a.dats else DATA / "dats"
    dats.mkdir(parents=True, exist_ok=True)
    for d in ("mods", "content", "backups"):
        (DATA / d).mkdir(exist_ok=True)
    if not list(dats.glob("client_*.dat")):
        sys.exit(f"Put your AC client DAT files (client_cell_1.dat, client_portal.dat, client_highres.dat, "
                 f"client_local_English.dat) in:\n  {dats}\nthen run again (or pass --dats DIR).")
    if compose("pull", check=False, dats=dats).returncode != 0:
        sys.exit("Could not pull the images (are you online, and are the ghcr.io/mossbuilds packages public?).")
    compose("up", "-d", dats=dats)
    ip = lan_ip()
    print("\n" + "=" * 46)
    print("  ACE server is starting (give it ~1 minute)")
    print(f"  Connect your client to:  {ip}  port 9000")
    print("  (same machine: 127.0.0.1 port 9000)")
    print("  Client example: acclient.exe -h 127.0.0.1:9000 -a <account> -v <password>")
    print("  Any new account name auto-creates; the FIRST account becomes admin.")
    print("=" * 46)


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
        compose("pull", "ace-server")
        compose("up", "-d", "ace-server")
    else:
        compose("pull")
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
    u = sub.add_parser("up"); u.add_argument("--dats")
    sub.add_parser("down"); sub.add_parser("status"); sub.add_parser("logs"); sub.add_parser("version")
    b = sub.add_parser("backup"); b.add_argument("--all", action="store_true")
    r = sub.add_parser("restore"); r.add_argument("file")
    up = sub.add_parser("update"); up.add_argument("what", nargs="?", default="all", choices=["server", "db", "all"])
    a = ap.parse_args()
    a.cmd = a.cmd or "up"
    if a.cmd == "up" and not hasattr(a, "dats"):
        a.dats = None
    if a.cmd == "version":
        print(f"acbuilds launcher {VERSION}")
    elif a.cmd == "up":
        cmd_up(a)
    elif a.cmd == "down":
        ensure_docker(); compose("down")
    elif a.cmd == "status":
        ensure_docker(); compose("ps")
    elif a.cmd == "logs":
        ensure_docker(); compose("logs", "-f", "ace-server", check=False)
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
    if IS_WIN and getattr(sys, "frozen", False) and len(sys.argv) == 1:
        input("\nPress Enter to close...")  # double-clicked: keep the window open
