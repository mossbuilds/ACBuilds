#!/usr/bin/env python3
"""ACBuilds self-heal engine, invoked by heal.sh (which holds the deploy lock and skip-switches).

Subcommands:
  run [--dry-run]                 check the stack, take at most one remediation action, update state + heal.json
  rollback-result --ok|--fail --version V   record the outcome of a rollback heal.sh ran itself (needs the deploy
                                             lock released, so heal.sh does it, not this script)
  status                          print the state file human-readably

Everything here is read-only except the docker/systemctl/hopper calls each rung fires. State lives in
heal-state.json next to this script's APP dir; stdlib only (this runs on the VPS as the 'acbuilds' user).
"""
import argparse, copy, datetime, json, os, subprocess, sys, urllib.request, urllib.error

APP = "/opt/acbuilds"
STATE_FILE = os.path.join(APP, "heal-state.json")
STATUS_JSON = os.path.join(APP, "status", "data", "heal.json")
HOPPER_TOKEN_FILE = os.path.join(APP, ".hopper_token")
HOPPER_URL = "https://loam.mossbuilds.xyz/hopper/add"
LAST_GOOD_URL = "https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/deploy/LAST_GOOD.txt"
BACKUP_KEEP = 14
INTERVAL_S = 180              # the timer's OnUnitActiveSec (3 min) - used for "still failing after ~6 min" math
STILL_FAILING_RUNS = 2        # a rung counts as failed if the component still fails this many runs after it fired
DOCKER_RESTART_COOLDOWN_S = 2 * 3600
REBOOT_COOLDOWN_S = 6 * 3600
REBOOT_MIN_UPTIME_S = 30 * 60
CRASH_LOOP_WINDOW_S = 3600
CRASH_LOOP_LIMIT = 3
HEALTHY_RUNS_TO_CLOSE = 2
ESCALATE_AFTER_S = STILL_FAILING_RUNS * INTERVAL_S - 60   # ~5 min: two timer runs, less jitter
NEEDS_HUMAN_RUNG = 6
# Failures a restart / rollback / docker restart / reboot can plausibly fix. Anything else (acb-status, disk) has only
# its targeted fix; if that does not work, restarting the game or rebooting the box will not either - alert instead.
CORE = {"ace-db", "ace-server", "ace-vr-server", "memory"}
# "World is now open" is printed ONCE per start. After log rotation (50m x 3) it is gone from a long-running server's
# log, so the first sighting per container start is recorded here (shared with the status page, acb_status.py).
WORLD_OPEN_FILE = os.path.join(APP, "status", "data", "world_open.json")

RUNG_NAMES = {0: "none", 1: "targeted", 2: "restart-all", 3: "rollback", 4: "restart-docker", 5: "reboot", 6: "needs_human"}


def now():
    return datetime.datetime.now(datetime.timezone.utc)


def iso(dt):
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_iso(s):
    if not s:
        return None
    try:
        return datetime.datetime.strptime(s, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=datetime.timezone.utc)
    except Exception:
        return None


def sh(cmd, timeout=30, cwd=None):
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, errors="replace", cwd=cwd)
        return r.returncode, r.stdout, r.stderr
    except Exception as e:
        return 1, "", str(e)


def dc(*args, timeout=180):
    return sh(["docker", "compose", "-f", os.path.join(APP, "docker-compose.yml")] + list(args), timeout=timeout, cwd=APP)


# ------------------------------------------------------------------ state
DEFAULT_STATE = {
    "incident_open": False,
    "incident_since": None,
    "rung": 0,
    "rung_last_action_at": None,
    "rung_fired_at": None,        # when the current rung's remediation actually fired
    "needs_human_since": None,
    "consecutive_healthy_runs": 0,
    "alerted": False,
    "last_check": None,
    "action_history": [],       # last 50: {ts, rung, action, component, result}
    "last_docker_restart": None,
    "last_reboot": None,
    "targeted_fire_counts": {},  # component -> [iso ts, ...] within the last hour
}


def load_state():
    try:
        with open(STATE_FILE) as f:
            d = json.load(f)
        s = copy.deepcopy(DEFAULT_STATE)
        s.update(d)
        return s
    except Exception:
        return copy.deepcopy(DEFAULT_STATE)


def save_state(state):
    tmp = STATE_FILE + ".tmp"
    with open(tmp, "w") as f:
        json.dump(state, f, indent=1)
    os.replace(tmp, STATE_FILE)


def record_action(state, rung, action, component, result):
    state["action_history"].append({"ts": iso(now()), "rung": rung, "action": action, "component": component, "result": result})
    state["action_history"] = state["action_history"][-50:]
    state["rung_last_action_at"] = iso(now())


def prune_fire_counts(state, component=None):
    cutoff = now() - datetime.timedelta(seconds=CRASH_LOOP_WINDOW_S)
    fc = state.setdefault("targeted_fire_counts", {})
    comps = [component] if component else list(fc.keys())
    for c in comps:
        fc[c] = [t for t in fc.get(c, []) if parse_iso(t) and parse_iso(t) > cutoff]


def note_fire(state, component):
    prune_fire_counts(state, component)
    state["targeted_fire_counts"].setdefault(component, []).append(iso(now()))


def fire_count(state, component):
    prune_fire_counts(state, component)
    return len(state.get("targeted_fire_counts", {}).get(component, []))


# ------------------------------------------------------------------ hopper
def hopper_post(text, source, tags):
    try:
        with open(HOPPER_TOKEN_FILE) as f:
            token = f.read().strip()
    except Exception:
        print(f"hopper post skipped (no {HOPPER_TOKEN_FILE}): {text}")
        return False
    if not token:
        print(f"hopper post skipped (empty token file): {text}")
        return False
    body = json.dumps({"text": text, "source": source, "tags": tags}).encode("utf-8")
    req = urllib.request.Request(HOPPER_URL, data=body, method="POST",
                                  headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            r.read()
        print(f"hopper posted: {text}")
        return True
    except Exception as e:
        print(f"hopper post FAILED: {e}: {text}")
        return False


# ------------------------------------------------------------------ checks
def container_inspect(name):
    code, out, _ = sh(["docker", "inspect", "-f",
                        '{{.State.Running}}|{{.State.StartedAt}}|'
                        '{{if .State.Health}}{{.State.Health.Status}}{{end}}|'
                        '{{index .Config.Labels "org.opencontainers.image.version"}}', name])
    if code != 0:
        return {"running": False}
    running, started, health, version = (out.strip().split("|") + ["", "", "", ""])[:4]
    return {"running": running == "true", "started": started, "health": health, "version": version}


def _load_world_open():
    try:
        with open(WORLD_OPEN_FILE) as f:
            return json.load(f)
    except Exception:
        return {}


def world_open_since(name, started_iso):
    """True once "World is now open" has appeared since this container's current start (sticky per start)."""
    key = (started_iso or "")[:19]
    if not key:
        return False
    marks = _load_world_open()
    if marks.get(name) == key:
        return True
    code, out, err = sh(["docker", "logs", "--since", started_iso, name], timeout=30)
    if "World is now open" not in ((out or "") + (err or "")):
        return False
    marks[name] = key
    try:
        os.makedirs(os.path.dirname(WORLD_OPEN_FILE), exist_ok=True)
        tmp = f"{WORLD_OPEN_FILE}.{os.getpid()}.tmp"
        with open(tmp, "w") as f:
            json.dump(marks, f)
        os.replace(tmp, WORLD_OPEN_FILE)
    except OSError:
        pass
    return True


def container_age_s(started_iso):
    try:
        t = started_iso.split(".")[0]
        if "Z" not in t:
            t += "Z"
        dt = datetime.datetime.strptime(t, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=datetime.timezone.utc)
        return (now() - dt).total_seconds()
    except Exception:
        return 9e9


def check_db():
    info = container_inspect("ace-db")
    ok = bool(info.get("running")) and info.get("health") == "healthy"
    return {"running": info.get("running", False), "healthy": info.get("health") == "healthy", "ok": ok}


def check_server(name):
    info = container_inspect(name)
    if not info.get("running"):
        return {"running": False, "world_open": False, "starting": False, "ok": False, "version": info.get("version", "")}
    age = container_age_s(info.get("started", ""))
    opened = world_open_since(name, info.get("started", ""))
    # started < 6 min ago and not open yet: still booting. Not ok, not failing - the ladder waits for it.
    starting = (not opened) and age < 360
    return {"running": True, "world_open": opened, "starting": starting, "ok": opened, "version": info.get("version", "")}


def check_disk():
    st = os.statvfs("/")
    used_pct = round(100.0 * (st.f_blocks - st.f_bfree) / st.f_blocks, 1) if st.f_blocks else 0.0
    return {"percent": used_pct, "ok": used_pct < 90}


def check_memory():
    mem = {}
    with open("/proc/meminfo") as f:
        for line in f:
            k, v = line.split(":", 1)
            mem[k] = int(v.split()[0])
    used_pct = round(100.0 * (mem["MemTotal"] - mem.get("MemAvailable", mem["MemTotal"])) / mem["MemTotal"], 1)
    return {"percent": used_pct, "ok": used_pct < 95}


def check_acb_status():
    code, out, _ = sh(["systemctl", "is-active", "acb-status"])
    active = out.strip() == "active"
    return {"active": active, "ok": active}


def check_udp_ports():
    code, out, _ = sh(["ss", "-uln"])
    have_9000 = ":9000 " in out or out.rstrip().endswith(":9000")
    have_9100 = ":9100 " in out or out.rstrip().endswith(":9100")
    # be lenient about column spacing - just look for the port token
    have_9000 = have_9000 or any(l.split()[-2:] and ":9000" in l for l in out.splitlines())
    have_9100 = have_9100 or any(l.split()[-2:] and ":9100" in l for l in out.splitlines())
    return {"9000": have_9000, "9100": have_9100, "ok": have_9000 and have_9100}


def run_checks():
    db = check_db()
    servers = {"ace-server": check_server("ace-server"), "ace-vr-server": check_server("ace-vr-server")}
    disk = check_disk()
    mem = check_memory()
    status = check_acb_status()
    # Informational only: docker-proxy binds the host port whether or not ACE is listening, and with the userland
    # proxy off nothing shows at all - so it can neither prove nor disprove health, and must never trigger a fix.
    ports = check_udp_ports()
    failing, pending = [], []
    if not db["ok"]:
        failing.append("ace-db")
    for name, s in servers.items():
        if s["ok"]:
            continue
        (pending if s["starting"] else failing).append(name)
    if not disk["ok"]:
        failing.append("disk")
    if not mem["ok"]:
        failing.append("memory")
    if not status["ok"]:
        failing.append("acb-status")
    return {"db": db, "servers": servers, "disk": disk, "memory": mem, "acb_status": status, "udp_ports": ports,
            "failing": failing, "pending": pending, "healthy": not failing and not pending}


# ------------------------------------------------------------------ targeted (rung 1) fixes
def wait_db_healthy(timeout_s=120):
    deadline = now() + datetime.timedelta(seconds=timeout_s)
    while now() < deadline:
        if check_db()["ok"]:
            return True
        sh(["sleep", "3"])
    return check_db()["ok"]


def targeted_fix(checks, dry_run):
    """Pick and fire ONE targeted fix for the highest-priority failure. Returns (component, description, ok)."""
    if not checks["db"]["ok"]:
        comp = "ace-db"
        if dry_run:
            return comp, "would: docker compose restart ace-db, wait healthy, restart both servers", True
        dc("restart", "ace-db")
        healthy = wait_db_healthy(120)
        dc("restart", "ace-server", "ace-vr-server")
        return comp, f"restarted ace-db (healthy={healthy}) then both game servers", healthy

    down = [n for n, s in checks["servers"].items() if not s["ok"]]
    if down:
        comp = ",".join(down)
        if dry_run:
            return comp, f"would: docker compose restart {' '.join(down)}", True
        code, out, err = dc("restart", *down)
        return comp, f"restarted {comp}", code == 0

    if not checks["disk"]["ok"]:
        comp = "disk"
        if dry_run:
            return comp, "would: prune images/builder, trim old backups, vacuum journal", True
        sh(["docker", "image", "prune", "-af"], timeout=300)
        sh(["docker", "builder", "prune", "-af"], timeout=300)
        backups = sorted((f for f in os.listdir(os.path.join(APP, "backups")) if f.startswith("ace-") and f.endswith(".sql.gz")),
                         reverse=True)
        for f in backups[BACKUP_KEEP:]:
            try:
                os.remove(os.path.join(APP, "backups", f))
            except OSError:
                pass
        sh(["sudo", "-n", "/usr/bin/journalctl", "--vacuum-size=200M"], timeout=120)
        return comp, "pruned docker images/builder cache, trimmed backups beyond newest 14, vacuumed journal", True

    if not checks["memory"]["ok"]:
        comp = "memory"
        if dry_run:
            return comp, "would: restart the game server container using the most memory", True
        code, out, _ = sh(["docker", "stats", "--no-stream", "--format", "{{.Name}}|{{.MemUsage}}"], timeout=30)
        best, best_mb = None, -1
        for line in out.splitlines():
            parts = line.split("|")
            if len(parts) != 2 or parts[0] not in ("ace-server", "ace-vr-server"):
                continue
            raw = parts[1].split("/")[0].strip()
            try:
                val = float(raw.rstrip("GiBMKi"))
                mb = val * 1024 if "GiB" in raw else val
            except ValueError:
                mb = 0
            if mb > best_mb:
                best, best_mb = parts[0], mb
        best = best or "ace-server"
        dc("restart", best)
        return best, f"restarted {best} (highest memory use)", True

    if not checks["acb_status"]["ok"]:
        comp = "acb-status"
        if dry_run:
            return comp, "would: sudo systemctl restart acb-status", True
        code, _, _ = sh(["sudo", "-n", "/usr/bin/systemctl", "restart", "acb-status"], timeout=30)
        return comp, "restarted acb-status", code == 0

    return "unknown", "no targeted fix matched a failing check", False


# ------------------------------------------------------------------ ladder
def fetch_last_good():
    try:
        with urllib.request.urlopen(LAST_GOOD_URL, timeout=15) as r:
            for line in r.read().decode("utf-8", "replace").splitlines():
                line = line.strip()
                if line and not line.startswith("#"):
                    return line
    except Exception:
        return None
    return None


def running_version():
    for name in ("ace-server", "ace-vr-server"):
        info = container_inspect(name)
        if info.get("version"):
            return info["version"]
    return None


def uptime_s():
    try:
        with open("/proc/uptime") as f:
            return float(f.read().split()[0])
    except Exception:
        return 0.0


def _finish(state, checks, dry_run, summary, rc=0):
    if not dry_run:
        save_state(state)
    write_status(state, checks)
    print(summary)
    return rc


def do_run(dry_run):
    state = load_state()
    checks = run_checks()
    state["last_check"] = checks

    if checks["healthy"]:
        state["consecutive_healthy_runs"] = state.get("consecutive_healthy_runs", 0) + 1
        summary = f"healthy (consecutive={state['consecutive_healthy_runs']})"
        if state["incident_open"] and state["consecutive_healthy_runs"] >= HEALTHY_RUNS_TO_CLOSE:
            was_alerted = state["alerted"]
            since = state["incident_since"]
            # targeted_fire_counts are deliberately NOT reset: they age out after an hour on their own, so a server
            # that keeps falling over across several short incidents still trips the crash-loop guard.
            state.update(incident_open=False, incident_since=None, rung=0, rung_fired_at=None,
                         rung_last_action_at=None, alerted=False, needs_human_since=None)
            if was_alerted and not dry_run:
                hopper_post(f"ACBuilds recovered: all checks healthy (incident since {since})", "acbuilds-heal", ["acbuilds"])
            summary = f"recovered, incident closed (was open since {since})"
        return _finish(state, checks, dry_run, summary)

    if not checks["failing"]:
        # only servers still booting after a (re)start: give them time, change nothing, do not count as healthy
        return _finish(state, checks, dry_run, f"waiting: {', '.join(checks['pending'])} starting")

    state["consecutive_healthy_runs"] = 0
    if not state["incident_open"]:
        state.update(incident_open=True, incident_since=iso(now()), rung=0, rung_fired_at=None, needs_human_since=None)

    rung = state.get("rung", 0)
    fired = parse_iso(state.get("rung_fired_at"))
    failing_txt = ", ".join(checks["failing"])
    if 1 <= rung < NEEDS_HUMAN_RUNG and fired and (now() - fired).total_seconds() < ESCALATE_AFTER_S:
        return _finish(state, checks, dry_run,
                       f"waiting: rung {rung} ({RUNG_NAMES[rung]}) fired {state['rung_fired_at']}, giving it time "
                       f"(failing: {failing_txt})")

    core = any(c in CORE for c in checks["failing"])
    if rung >= NEEDS_HUMAN_RUNG:
        rung = NEEDS_HUMAN_RUNG
    elif rung == 0:
        rung = 1
    elif not core:
        rung = NEEDS_HUMAN_RUNG   # acb-status / disk: nothing past the targeted fix helps
    else:
        rung += 1
    if rung == 1 and any(fire_count(state, c) >= CRASH_LOOP_LIMIT for c in checks["failing"]):
        rung = 2 if core else NEEDS_HUMAN_RUNG

    action_desc, component, ok, rollback_version, fired_now = "", "", True, None, False

    if rung == 1:
        component, action_desc, ok = targeted_fix(checks, dry_run)
        fired_now = True
        if not dry_run:
            for c in component.split(","):
                note_fire(state, c)

    elif rung == 2:
        component = "all"
        fired_now = True
        if dry_run:
            action_desc = "would: docker compose restart of ace-db ace-server ace-vr-server"
        else:
            code, _, _ = dc("restart", "ace-db", "ace-server", "ace-vr-server")
            action_desc = "restarted all three containers"
            ok = code == 0

    elif rung == 3:
        last_good = fetch_last_good()
        running = running_version()
        if not last_good:
            action_desc = "no LAST_GOOD.txt available - cannot roll back"
            rung = 4
        elif running and last_good == running:
            rung = 4  # already on LAST_GOOD - nothing to roll back to, escalate this run
        else:
            component = "rollback"
            fired_now = True
            if dry_run:
                action_desc = f"would: deploy.sh --rollback {last_good} (running={running})"
            else:
                action_desc = f"rollback to {last_good} requested (running={running})"
                rollback_version = last_good

    if rung == 4:
        component = "docker"
        last = parse_iso(state.get("last_docker_restart"))
        if last and (now() - last).total_seconds() < DOCKER_RESTART_COOLDOWN_S:
            action_desc = "docker restart on cooldown (< 2h since last one) - escalating"
            rung = 5
        else:
            fired_now = True
            if dry_run:
                action_desc = "would: sudo systemctl restart docker"
            else:
                code, _, _ = sh(["sudo", "-n", "/usr/bin/systemctl", "restart", "docker"], timeout=90)
                action_desc = "restarted the docker daemon"
                ok = code == 0
                state["last_docker_restart"] = iso(now())

    if rung == 5:
        component = "host"
        last = parse_iso(state.get("last_reboot"))
        up = uptime_s()
        if last and (now() - last).total_seconds() < REBOOT_COOLDOWN_S:
            action_desc = "reboot on cooldown (< 6h since last one) - escalating"
            rung = NEEDS_HUMAN_RUNG
        elif up < REBOOT_MIN_UPTIME_S:
            action_desc = f"uptime only {int(up)}s (< 30 min) - refusing to reboot again so soon, escalating"
            rung = NEEDS_HUMAN_RUNG
        elif dry_run:
            action_desc = "would: sudo reboot"
        else:
            # record BEFORE rebooting - if the box does not come back, the state still shows we tried
            state.update(last_reboot=iso(now()), rung=5, rung_fired_at=iso(now()))
            record_action(state, 5, "reboot", component, "fired")
            write_status(state, checks)
            save_state(state)
            print("rung=5 (reboot) component=host: rebooting the host")
            sh(["sudo", "-n", "/usr/sbin/reboot"], timeout=10)
            return 0

    if rung == NEEDS_HUMAN_RUNG:
        component = "operator"
        if not state.get("needs_human_since"):
            state["needs_human_since"] = iso(now())
        if not state["alerted"]:
            rungs_tried = ", ".join(sorted({f"{h['rung']} ({RUNG_NAMES.get(h['rung'], '?')})"
                                            for h in state["action_history"]
                                            if parse_iso(h.get("ts")) and state["incident_since"]
                                            and h["ts"] >= state["incident_since"]})) or "none"
            text = (f"URGENT from ACBuilds: {failing_txt} failing since {state['incident_since']}; "
                    f"self-heal tried: {rungs_tried}. SSH root@89.117.147.78, see /opt/acbuilds/heal.log")
            if dry_run:
                action_desc = f"would post hopper alert: {text}"
            else:
                posted = hopper_post(text, "acbuilds-heal", ["urgent", "acbuilds"])
                state["alerted"] = posted
                action_desc = f"needs_human - hopper alert {'posted' if posted else 'FAILED to post'}"
        else:
            action_desc = "needs_human - already alerted, re-checking"

    state["rung"] = rung
    if fired_now and not dry_run:
        state["rung_fired_at"] = iso(now())
    summary = f"rung={rung} ({RUNG_NAMES.get(rung, '?')}) component={component}: {action_desc}"
    if rollback_version and not dry_run:
        # heal.sh performs the rollback itself (the deploy lock must be released first), then calls rollback-result
        record_action(state, rung, "rollback requested", component, "pending")
        _finish(state, checks, dry_run, summary)
        print(f"ROLLBACK_NEEDED {rollback_version}")
        return 42
    if not dry_run and action_desc != "needs_human - already alerted, re-checking":   # no history spam every 3 min
        record_action(state, rung, action_desc, component, "ok" if ok else "failed")
    return _finish(state, checks, dry_run, summary)


def do_rollback_result(ok, version):
    state = load_state()
    record_action(state, state.get("rung", 3), f"rollback to {version}", "rollback", "ok" if ok else "failed")
    save_state(state)
    checks = state.get("last_check") or {"failing": ["unknown"]}
    write_status(state, checks)
    print(f"rollback to {version}: {'ok' if ok else 'FAILED'}")


def write_status(state, checks):
    if not state["incident_open"]:
        st = "healthy"
    elif state.get("rung", 0) >= NEEDS_HUMAN_RUNG:
        st = "needs_human"
    else:
        st = "healing"
    d = {
        "state": st,
        "rung": state["rung"],
        "rung_name": RUNG_NAMES.get(state["rung"], "?"),
        "failing": checks.get("failing", []),
        "pending": checks.get("pending", []),
        "last_action": (state["action_history"][-1]["action"] if state["action_history"] else None),
        "last_action_at": (state["action_history"][-1]["ts"] if state["action_history"] else None),
        "incident_since": state["incident_since"],
        "needs_human_since": state.get("needs_human_since"),
        "alerted": state["alerted"],
        "checked_at": iso(now()),
    }
    os.makedirs(os.path.dirname(STATUS_JSON), exist_ok=True)
    tmp = STATUS_JSON + ".tmp"
    with open(tmp, "w") as f:
        json.dump(d, f, indent=1)
    os.replace(tmp, STATUS_JSON)


def do_status():
    state = load_state()
    print(json.dumps(state, indent=1))


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--dry-run", action="store_true")
    rr = sub.add_parser("rollback-result")
    g = rr.add_mutually_exclusive_group(required=True)
    g.add_argument("--ok", action="store_true")
    g.add_argument("--fail", action="store_true")
    rr.add_argument("--version", required=True)
    sub.add_parser("status")
    a = ap.parse_args()

    if a.cmd == "run":
        sys.exit(do_run(a.dry_run))
    elif a.cmd == "rollback-result":
        do_rollback_result(a.ok, a.version)
    elif a.cmd == "status":
        do_status()


if __name__ == "__main__":
    main()
