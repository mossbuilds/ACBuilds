#!/usr/bin/env python3
"""ACBuilds status service: game-server health, database stats, players and where they are.

Runs on the host next to the Docker containers (Python standard library only), serves a page and a JSON API on 127.0.0.1
(put a reverse proxy with TLS + auth in front of it). Read-only: it only runs SELECTs and `docker inspect/logs/exec`.

  python3 acb_status.py [--port 8618] [--data DIR]        DIR holds locations.xml and cod_locations.xml
  python3 acb_status.py --once                            print the JSON once and exit (debug)
"""
import argparse, datetime, hashlib, html, json, math, os, re, secrets, subprocess, sys, threading, time, urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HERE = Path(__file__).resolve().parent
DB = os.environ.get("ACB_DB_CONTAINER", "ace-db")
SERVERS = [  # key, label, container, game port, shard database
    ("stock", "Normal game", os.environ.get("ACB_STOCK_CONTAINER", "ace-server"), 9000, "ace_shard"),
    ("vr", "PC VR server", os.environ.get("ACB_VR_CONTAINER", "ace-vr-server"), 9100, "ace_shard_vr"),
]
HERITAGE = {0: "Unknown", 1: "Aluvian", 2: "Gharu'ndim", 3: "Sho", 4: "Viamontian", 5: "Shadowbound", 6: "Gearknight",
            7: "Tumerok", 8: "Lugian", 9: "Empyrean", 10: "Penumbraen", 11: "Undead", 12: "Olthoi", 13: "Olthoi Acid"}
ACCESS = {0: "Player", 1: "Advocate", 2: "Sentinel", 3: "Envoy", 4: "Developer", 5: "Admin"}
PLACE_TYPES = {"Town", "Village", "Landmark", "Dungeon", "Outpost", "Allegiance Hall", "Lifestone", "Bindstone",
               "Town Building", "Meeting Hall", "Lanmark", "Nendor"}
TOWN_TYPES = {"Town", "Village"}

# ------------------------------------------------------------------ mods page (public: names/descriptions only)
MODS_DIR = Path(os.environ.get("ACB_MODS_DIR", "/opt/acbuilds/mods"))
MODS_VR_DIR = Path(os.environ.get("ACB_MODS_VR_DIR", "/opt/acbuilds/mods-vr"))
STATUS_MD_URL = os.environ.get("ACB_STATUS_MD_URL", "https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/mods-proposed/STATUS.md")
IDEAS_MD_URL = os.environ.get("ACB_IDEAS_MD_URL", "https://raw.githubusercontent.com/mossbuilds/ACBuilds/main/mods-proposed/IDEAS.md")
HOPPER_LIST_URL = os.environ.get("ACB_HOPPER_LIST_URL", "https://loam.mossbuilds.xyz/hopper/list.json")
HOPPER_TOKEN_FILE = Path(os.environ.get("ACB_HOPPER_TOKEN_FILE", "/opt/acbuilds/.hopper_token"))
MODS_CACHE_TTL = int(os.environ.get("ACB_MODS_CACHE_TTL", "300"))
COMMUNITY_FILE = Path(os.environ.get("ACB_COMMUNITY_FILE", "/opt/acbuilds/status/data/community.json"))
COMMUNITY_MAX_IDEAS = 500
IDEA_RATE_LIMIT = (5, 3600)    # max 5 ideas per hour per voter
VOTE_RATE_LIMIT = (60, 3600)   # max 60 votes per hour per voter


def mod_servers():
    """(key, label, port, dir) read live so tests can override the module-level dir constants."""
    return [("stock", "Normal server", 9000, MODS_DIR), ("vr", "VR server", 9100, MODS_VR_DIR)]


# ------------------------------------------------------------------ helpers
def sh(cmd, timeout=25):
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, errors="replace")
        return r.returncode, r.stdout, r.stderr
    except Exception as e:  # docker missing / timeout
        return 1, "", str(e)


def sql(query, timeout=30):
    """Run a SELECT inside the database container (root over the container's own socket) and return rows of strings."""
    code, out, err = sh(["docker", "exec", DB, "mariadb", "-uroot", "-N", "-B", "-e", query], timeout)
    if code != 0:
        raise RuntimeError((err or out).strip().splitlines()[-1][:200] if (err or out).strip() else "query failed")
    return [line.split("\t") for line in out.splitlines() if line != ""]


def num(v, default=0):
    try:
        return int(v)
    except Exception:
        try:
            return float(v)
        except Exception:
            return default


# ------------------------------------------------------------------ locations
class Places:
    """Named places from the Crossroads of Dereth XML files, plus dungeon landblock -> name."""

    def __init__(self, data_dir):
        self.places = []       # (lat, lon, name, type, extra)
        self.dungeons = {}     # "015B" -> dict(name, levels, monsters, description)
        self.loaded = []
        d = Path(data_dir)

        by_name = {}  # (name, type) -> coordinates already added; the two files overlap, so skip near-duplicates

        def add(lat, lon, name, typ, extra):
            if lat == 0 and lon == 0:
                return
            key = (name.lower(), typ)
            for (a, b) in by_name.get(key, ()):
                if math.hypot(a - lat, b - lon) < 1.5:
                    return
            by_name.setdefault(key, []).append((lat, lon))
            self.places.append((lat, lon, name, typ, extra))

        cod = d / "cod_locations.xml"
        if cod.exists():
            t = cod.read_text(encoding="utf-8", errors="replace")
            for b in re.findall(r"<location>(.*?)</location>", t, re.S):
                g = lambda tag: html.unescape((re.search(rf"<{tag}>(.*?)</{tag}>", b, re.S) or [None, ""])[1]).strip()
                try:
                    lat, lon = float(g("latitude") or 0), float(g("longitude") or 0)
                except ValueError:
                    continue
                name, typ = g("name"), g("type")
                extra = {"levels": g("restrictions"), "monsters": g("monsters"), "description": g("description")[:240]}
                did = g("dungeon_id").upper().zfill(4) if g("dungeon_id") not in ("", "0") else ""
                if did and len(did) == 4:
                    self.dungeons.setdefault(did, {"name": name, **extra})
                if typ in PLACE_TYPES:
                    add(lat, lon, name, "Landmark" if typ == "Lanmark" else typ, extra)
            self.loaded.append(f"cod_locations.xml ({len(self.places)} places, {len(self.dungeons)} dungeon landblocks)")
        loc = d / "locations.xml"
        if loc.exists():
            t = loc.read_text(encoding="utf-8-sig", errors="replace")
            n0 = len(self.places)
            for m in re.finditer(r'<loc id="\d+" name="(.*?)" type="(.*?)" NS="(.*?)" EW="(.*?)">(.*?)</loc>', t, re.S):
                name, typ = html.unescape(m.group(1)), m.group(2)
                try:
                    lat, lon = float(m.group(3)), float(m.group(4))
                except ValueError:
                    continue
                if typ in PLACE_TYPES:
                    add(lat, lon, name, typ, {"levels": "", "monsters": "", "description": html.unescape(m.group(5))[:240]})
            self.loaded.append(f"locations.xml (+{len(self.places) - n0} extra places)")
        self.towns = [p for p in self.places if p[3] in TOWN_TYPES]

    @staticmethod
    def coords(cell, x, y):
        """Map coordinates (north, east) for an outdoor position: 1 map unit = 240 game units, Dereth spans -102..+102."""
        lb = (cell >> 16) & 0xFFFF
        lbx, lby = lb >> 8, lb & 0xFF
        return (lby * 192 + y) / 240.0 - 102.0, (lbx * 192 + x) / 240.0 - 102.0

    @staticmethod
    def compass(dn, de):
        if abs(dn) < 0.05 and abs(de) < 0.05:
            return "at"
        ang = (math.degrees(math.atan2(dn, de)) + 360) % 360  # 0 = east, 90 = north
        return ["E", "NE", "N", "NW", "W", "SW", "S", "SE"][int(((ang + 22.5) % 360) // 45)]

    def nearest(self, lat, lon, pool=None):
        best, bd = None, 1e9
        for p in (pool if pool is not None else self.places):
            d = math.hypot(p[0] - lat, p[1] - lon)
            if d < bd:
                best, bd = p, d
        return best, bd

    KNOWN_AREAS = {"7F03": "Training Academy (new-character tutorial area)"}
    BUILDING_ANCHORS = {"Town", "Village", "Landmark", "Allegiance Hall", "Town Building", "Meeting Hall"}

    def describe(self, cell, x, y):
        """Human description of a saved position."""
        lb = (cell >> 16) & 0xFFFF
        c = cell & 0xFFFF
        lbhex = f"{lb:04X}"
        out = {"cell": f"0x{cell:08X}", "landblock": lbhex, "indoors": c >= 0x100, "x": round(x, 1), "y": round(y, 1)}
        if c >= 0x100:
            if lbhex in self.dungeons:
                d = self.dungeons[lbhex]
                out.update(kind="dungeon", place=d["name"], detail=" - ".join(v for v in (
                    ("levels " + d["levels"]) if d["levels"] else "", d["monsters"][:110]) if v))
                return out
            if lbhex in self.KNOWN_AREAS:
                out.update(kind="dungeon", place=self.KNOWN_AREAS[lbhex], detail="")
                return out
            # a building/house interior shares the landblock of the town around it: anchor on the landblock centre
            lat, lon = self.coords(lb << 16, 96.0, 96.0)
            p, d = self.nearest(lat, lon, [q for q in self.places if q[3] in self.BUILDING_ANCHORS])
            if p is not None and d <= 0.9:
                out.update(kind="building", place="Inside a building near " + p[2], detail="", ns=round(lat, 1), ew=round(lon, 1),
                           coords=f"{abs(lat):.1f}{'N' if lat >= 0 else 'S'}, {abs(lon):.1f}{'E' if lon >= 0 else 'W'}")
                return out
            out.update(kind="unmapped", place="Dungeon / instanced area", detail=f"landblock {lbhex} is not in the location database")
            return out
        lat, lon = self.coords(cell, x, y)
        out["ns"], out["ew"] = round(lat, 1), round(lon, 1)
        out["coords"] = f"{abs(lat):.1f}{'N' if lat >= 0 else 'S'}, {abs(lon):.1f}{'E' if lon >= 0 else 'W'}"
        p, d = self.nearest(lat, lon)
        t, td = self.nearest(lat, lon, self.towns)
        out["kind"] = "outdoors"
        if p:
            out["place"] = p[2] if d < 0.15 else f"{d:.1f} {self.compass(lat - p[0], lon - p[1])} of {p[2]}"
            out["place_type"] = p[3]
            out["detail"] = (p[4].get("monsters") or "")[:110]
        if t:
            out["town"] = f"{t[2]} ({td:.1f} away)"
        return out


# ------------------------------------------------------------------ collectors
def container_info(name):
    code, out, _ = sh(["docker", "inspect", "-f", "{{.State.Running}}|{{.State.StartedAt}}|{{.RestartCount}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}", name])
    if code != 0:
        return {"running": False, "error": "container not found"}
    running, started, restarts, health = (out.strip().split("|") + ["", "", "", ""])[:4]
    info = {"running": running == "true", "restarts": num(restarts), "health": health}
    try:
        st = datetime.datetime.fromisoformat(started.replace("Z", "+00:00").split(".")[0] + "+00:00")
        info["started"] = st.isoformat()
        info["uptime_s"] = max(0, int((datetime.datetime.now(datetime.timezone.utc) - st).total_seconds()))
    except Exception:
        pass
    return info


LOG_TS = re.compile(r"^(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)")
WORLD_OPEN_JSON = Path(os.environ.get("ACB_WORLD_OPEN_JSON", "/opt/acbuilds/status/data/world_open.json"))


def world_open_now(container):
    """Has the world opened since this container's CURRENT start? "World is now open" is printed once per start:
    after `docker compose restart` the old start's line is still in the log (a false "open"), and after log rotation
    (50m x 3) a long-running server's line is gone (a false "closed"). So: look only at the log since the current
    start, and remember the first sighting per start in world_open.json (shared with deploy/vps/heal_lib.py)."""
    code, started, _ = sh(["docker", "inspect", "-f", "{{.State.StartedAt}}", container])
    started = (started or "").strip()
    if code != 0 or not started:
        return False
    key = started[:19]
    try:
        marks = json.loads(WORLD_OPEN_JSON.read_text())
    except Exception:
        marks = {}
    if marks.get(container) == key:
        return True
    code, out, err = sh(["docker", "logs", "--since", started, container], 40)
    if "World is now open" not in (out or "") + (err or ""):
        return False
    marks[container] = key
    try:
        WORLD_OPEN_JSON.parent.mkdir(parents=True, exist_ok=True)
        tmp = WORLD_OPEN_JSON.with_name(f"{WORLD_OPEN_JSON.name}.{os.getpid()}.tmp")
        tmp.write_text(json.dumps(marks))
        os.replace(tmp, WORLD_OPEN_JSON)
    except OSError:
        pass
    return True



def parse_logs(container):
    """World-open state, server version and who is connected, from the container log."""
    code, out, err = sh(["docker", "logs", container], 40)
    text = (out or "") + (err or "")
    res = {"world_open": False, "binary": "", "errors_24h": 0, "online": {}, "last_event": ""}
    events = {}  # account -> (state, time)
    cutoff = (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(hours=24)).strftime("%Y-%m-%d %H:%M:%S")
    for line in text.splitlines():
        m = LOG_TS.match(line)
        ts = m.group(1) if m else ""
        if "Current Server Binary:" in line:
            res["binary"] = line.split("Current Server Binary:")[1].strip()[:80]
        mc = re.search(r"client (\S+) connected with verified password", line)
        if mc:
            events[mc.group(1)] = ("on", ts)
        md = re.search(r"Session \S+ dropped\. Account: ([^,]*), Player: ([^,]*),", line)
        if md and md.group(1).strip():
            events[md.group(1).strip()] = ("off", ts)
        if ts >= cutoff and re.search(r" ERROR[: ]|Unhandled exception", line):
            res["errors_24h"] += 1
        if ts:
            res["last_event"] = ts
    res["world_open"] = world_open_now(container)
    res["online"] = {a: t for a, (s, t) in events.items() if s == "on"}
    return res


def db_summary():
    d = {}
    d["sizes"] = {r[0]: float(r[1]) for r in sql(
        "select table_schema, round(sum(data_length+index_length)/1048576,1) from information_schema.tables "
        "where table_schema like 'ace\\_%' group by 1")}
    a = sql("select count(*), sum(accessLevel>=1), sum(create_Time >= now() - interval 1 day), sum(banned_Time is not null and ban_Expire_Time > now()) "
            "from ace_auth.account")[0]
    d["accounts"] = {"total": num(a[0]), "staff": num(a[1]), "new_24h": num(a[2]), "banned": num(a[3])}
    try:
        d["world_weenies"] = num(sql("select count(*) from ace_world.weenie")[0][0])
    except Exception:
        d["world_weenies"] = None
    return d


def characters(shard, places):
    q = (
        "select c.id, c.name, coalesce(a.accountName,''), coalesce(a.accessLevel,0), c.total_Logins, c.last_Login_Timestamp, "
        "coalesce(l.value,1), coalesce(h.value,0), coalesce(p.obj_Cell_Id,0), coalesce(p.origin_X,0), coalesce(p.origin_Y,0) "
        f"from {shard}.`character` c "
        "left join ace_auth.account a on a.accountId = c.account_Id "
        f"left join {shard}.biota_properties_int l on l.object_Id = c.id and l.type = 25 "
        f"left join {shard}.biota_properties_int h on h.object_Id = c.id and h.type = 188 "
        f"left join {shard}.biota_properties_position p on p.object_Id = c.id and p.position_Type = 1 "
        "where c.is_Deleted = 0 order by c.last_Login_Timestamp desc"
    )
    rows = []
    for r in sql(q):
        cid, name, acct, acc, logins, last, lvl, her, cell, x, y = r
        cell = num(cell)
        rows.append({
            "id": num(cid), "name": name.lstrip("+"), "account": acct, "staff": ACCESS.get(num(acc), "Player") if num(acc) else "",
            "logins": num(logins), "last_login": num(last), "level": num(lvl, 1), "heritage": HERITAGE.get(num(her), "Unknown"),
            "location": places.describe(cell, float(x), float(y)) if cell else None,
        })
    return rows


def shard_counts(shard):
    return {"characters": num(sql(f"select count(*) from {shard}.`character` where is_Deleted = 0")[0][0]),
            "objects": num(sql(f"select count(*) from {shard}.biota")[0][0])}


def host_stats():
    d = {}
    try:
        d["load"] = [float(x) for x in Path("/proc/loadavg").read_text().split()[:3]]
        mem = {k: int(v.split()[0]) for k, v in (l.split(":", 1) for l in Path("/proc/meminfo").read_text().splitlines())}
        d["mem_total_mb"] = mem["MemTotal"] // 1024
        d["mem_avail_mb"] = mem["MemAvailable"] // 1024
        d["uptime_s"] = int(float(Path("/proc/uptime").read_text().split()[0]))
    except Exception:
        pass
    return d


def db_health():
    """{running, healthy} for ace-db using the same docker inspect pattern deploy.sh's db-wait loop uses."""
    code, out, _ = sh(["docker", "inspect", "-f", "{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{end}}", DB])
    if code != 0:
        return {"running": False, "healthy": False}
    running, health = (out.strip().split("|") + ["", ""])[:2]
    return {"running": running == "true", "healthy": health == "healthy"}


def disk_percent(path="/opt/acbuilds"):
    try:
        st = os.statvfs(path)
        used = st.f_blocks - st.f_bfree
        return round(100.0 * used / st.f_blocks, 1) if st.f_blocks else None
    except Exception:
        return None


HEAL_JSON = Path(os.environ.get("ACB_HEAL_JSON", "/opt/acbuilds/status/data/heal.json"))


def heal_status():
    """The on-box self-heal state (deploy/vps/heal.sh), or None if it has never run / isn't installed."""
    try:
        return json.loads(HEAL_JSON.read_text())
    except Exception:
        return None


def health():
    """A single pass/fail view for CI and the scheduled healthcheck workflow: is the whole stack actually serving."""
    now = datetime.datetime.now(datetime.timezone.utc)
    out = {"checked_at": now.strftime("%Y-%m-%dT%H:%M:%SZ")}
    servers = {}
    for key, _label, cont, _port, _shard in SERVERS:
        info = container_info(cont)
        running = bool(info.get("running"))
        world_open = parse_logs(cont)["world_open"] if running else False
        servers["ace_server" if key == "stock" else "ace_vr_server"] = {"running": running, "world_open": world_open}
    out.update(servers)
    out["ace_db"] = db_health()
    stats = docker_mem()
    load = host_stats().get("load")
    out["cpu_percent"] = round(load[0] * 100 / (os.cpu_count() or 1), 1) if load else None
    mem = host_stats()
    out["ram_percent"] = round(100.0 * (mem["mem_total_mb"] - mem["mem_avail_mb"]) / mem["mem_total_mb"], 1) if mem.get("mem_total_mb") else None
    out["disk_percent"] = disk_percent()
    out["heal"] = heal_status()
    out["overall_healthy"] = bool(
        out["ace_server"]["running"] and out["ace_server"]["world_open"]
        and out["ace_vr_server"]["running"] and out["ace_vr_server"]["world_open"]
        and out["ace_db"]["running"] and out["ace_db"]["healthy"]
    )
    return out


def docker_mem():
    code, out, _ = sh(["docker", "stats", "--no-stream", "--format", "{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}"], 40)
    m = {}
    for line in out.splitlines():
        p = line.split("|")
        if len(p) == 3:
            m[p[0]] = {"cpu": p[1], "mem": p[2].split("/")[0].strip()}
    return m


def collect(places):
    now = time.time()
    data = {"generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "errors": [], "servers": [], "players": [],
            "locations_loaded": places.loaded,
            "towns": [[q[2], q[0], q[1]] for q in places.towns if q[3] == "Town"]}
    stats = docker_mem()
    dbinfo = container_info(DB)
    dbinfo["stats"] = stats.get(DB, {})
    data["database"] = {"container": dbinfo}
    try:
        data["database"].update(db_summary())
    except Exception as e:
        data["errors"].append(f"database summary: {e}")
    all_players = []
    for key, label, cont, port, shard in SERVERS:
        s = {"key": key, "label": label, "container": cont, "port": port, "shard": shard}
        s.update(container_info(cont))
        s["stats"] = stats.get(cont, {})
        lg = parse_logs(cont) if s.get("running") else {"world_open": False, "online": {}, "binary": "", "errors_24h": 0, "last_event": ""}
        s.update({k: lg[k] for k in ("world_open", "binary", "errors_24h")})
        try:
            s.update(shard_counts(shard))
            chars = characters(shard, places)
        except Exception as e:
            data["errors"].append(f"{label}: {e}")
            chars = []
        online_accounts = lg["online"]
        used = set()
        for c in chars:  # the most recently used character of a connected account is the one in the world
            c["server"], c["server_key"] = label, key
            c["online"] = False
            if c["account"] in online_accounts and c["account"] not in used:
                connected = online_accounts[c["account"]]
                try:
                    ct = datetime.datetime.strptime(connected, "%Y-%m-%d %H:%M:%S").replace(tzinfo=datetime.timezone.utc).timestamp()
                except Exception:
                    ct = 0
                if c["last_login"] and c["last_login"] >= ct - 120:
                    c["online"] = True
                    used.add(c["account"])
        s["players_online"] = sum(1 for c in chars if c["online"])
        s["accounts_connected"] = len(online_accounts)
        s["status"] = ("down" if not s.get("running") else "starting" if not s["world_open"] else "up")
        all_players.extend(chars)
        data["servers"].append(s)
    all_players.sort(key=lambda c: (not c["online"], -c["last_login"]))
    data["players"] = all_players
    data["host"] = host_stats()
    data["database"]["characters_total"] = sum(s.get("characters", 0) for s in data["servers"])
    data["database"]["players_online"] = sum(s["players_online"] for s in data["servers"])
    data["collect_seconds"] = round(time.time() - now, 2)
    return data


# ------------------------------------------------------------------ mods: collectors + cache
_MODS_CACHE = {}  # key -> (fetched_at, ("ok", value) | ("error", message))


def _cached(key, fn):
    now = time.time()
    hit = _MODS_CACHE.get(key)
    if hit and now - hit[0] < MODS_CACHE_TTL:
        return hit[1]
    try:
        val = ("ok", fn())
    except Exception as e:
        val = ("error", str(e)[:200])
    _MODS_CACHE[key] = (now, val)
    return val


def name_key(name):
    """Case- and punctuation-insensitive key for matching a mod name across sources (spaces, _, -, markdown **)."""
    return re.sub(r"[^a-z0-9]+", "", (name or "").lower())


def deployed_mods(mods_dir):
    """Mod name + Enabled flag for each subfolder of a mounted mods dir. Never returns paths beyond the mod name."""
    out = []
    try:
        for p in sorted(Path(mods_dir).iterdir(), key=lambda p: p.name.lower()):
            if not p.is_dir():
                continue
            enabled = False
            meta = p / "Meta.json"
            if meta.exists():
                try:
                    j = json.loads(meta.read_text(encoding="utf-8", errors="replace"))
                    enabled = bool(j.get("Enabled"))
                except Exception:
                    enabled = False
            out.append({"name": p.name, "enabled": enabled})
    except OSError:
        pass
    return out


def fetch_text(url, timeout=10):
    with urllib.request.urlopen(url, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def parse_status_md(text):
    """STATUS.md rows: `Name | READY (compiled; ...) | date | summary`. Skip header/blank/non-pipe lines."""
    rows = []
    for line in text.splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "|" not in line:
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) < 2 or not parts[0]:
            continue
        name, field2 = parts[0], parts[1]
        status = (field2.split() or [""])[0]
        if len(parts) >= 4 and parts[3]:
            summary = parts[3]
        else:
            m = re.search(r"\((.*)\)", field2)
            summary = (m.group(1) if m else field2)
        rows.append({"name": name, "status": status, "summary": summary[:160]})
    return rows


IDEA_LINE = re.compile(r"^(\d+)\.\s+(.+?)\s*\(feas\s*(\d+)\)\s*-\s*(.*)$")


def parse_ideas_md(text):
    """IDEAS.md numbered entries: `61. LuminanceLedger (feas 5) - Read-only player command ...`."""
    out = []
    for line in text.splitlines():
        m = IDEA_LINE.match(line.strip())
        if not m:
            continue
        num, name, feas, rest = m.groups()
        desc = rest.strip()
        cut = desc.find(". ")
        if cut != -1:
            desc = desc[:cut + 1]
        out.append({"num": int(num), "name": name.strip().strip("*").strip(), "feas": int(feas), "desc": desc[:200].strip()})
    return out


def fetch_hopper_ideas():
    """Hopper items tagged acbuilds/ac-event, open/blocked/building. None (skip quietly) if the token is unavailable."""
    try:
        token = HOPPER_TOKEN_FILE.read_text(encoding="utf-8").strip()
    except OSError:
        return None
    if not token:
        return None
    req = urllib.request.Request(HOPPER_LIST_URL, headers={"Authorization": f"Bearer {token}"})
    with urllib.request.urlopen(req, timeout=10) as r:
        d = json.loads(r.read().decode("utf-8", "replace"))
    out = []
    for i in d.get("ideas", []):
        tags = i.get("tags") or []
        if not any(t in ("acbuilds", "ac-event") for t in tags):
            continue
        if i.get("status") not in ("open", "blocked", "building"):
            continue
        out.append({"id": i.get("id"), "status": i.get("status"), "text": (i.get("text") or "")[:220]})
    return out


CTRL_CHARS = re.compile(r"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]")
WHITESPACE = re.compile(r"\s+")


def clean_text(s, max_len):
    s = CTRL_CHARS.sub("", str(s or ""))
    s = WHITESPACE.sub(" ", s).strip()
    return s[:max_len]


class CommunityStore:
    """Player-submitted mod ideas and votes - a public, unauthenticated feature. Nothing here is scheduled to be
    built; it is a suggestion box only. Never forwarded to the hopper. One JSON file, atomic writes, one lock."""

    def __init__(self, path):
        self.path = Path(path)
        self.lock = threading.Lock()
        self._idea_hits = {}   # voter_hash -> [timestamps] (idea submissions)
        self._vote_hits = {}   # voter_hash -> [timestamps] (votes)

    def _load(self):
        try:
            d = json.loads(self.path.read_text(encoding="utf-8"))
        except Exception:
            d = {}
        d.setdefault("ideas", [])
        d.setdefault("votes", {})
        if not d.get("salt"):
            d["salt"] = secrets.token_hex(16)
        return d

    def _save(self, d):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        tmp = self.path.with_name(f"{self.path.name}.{os.getpid()}.tmp")
        tmp.write_text(json.dumps(d), encoding="utf-8")
        os.replace(tmp, self.path)

    def voter_hash(self, ip):
        # the salt must be created and saved ONCE - _load() makes a fresh random one whenever the file has none, so
        # without persisting it here the first voter's hash would differ from their later ones (a free second vote)
        with self.lock:
            d = self._load()
            try:
                on_disk = json.loads(self.path.read_text(encoding="utf-8")).get("salt")
            except Exception:
                on_disk = None
            if on_disk != d["salt"]:
                self._save(d)
            return hashlib.sha256((d["salt"] + ip).encode("utf-8")).hexdigest()[:16]

    @staticmethod
    def _rate_ok(hits, voter, limit):
        n, window = limit
        now = time.time()
        lst = [t for t in hits.get(voter, ()) if now - t < window]
        hits[voter] = lst
        if len(lst) >= n:
            return False
        lst.append(now)
        return True

    def ideas(self, include_hidden=False):
        with self.lock:
            d = self._load()
            votes = d["votes"]
            out = []
            for i in d["ideas"]:
                if i.get("hidden") and not include_hidden:
                    continue
                j = dict(i)
                j["votes"] = len(votes.get(f"community:{i['id']}", []))
                out.append(j)
            return out

    def add_idea(self, title, desc, voter):
        with self.lock:
            if not self._rate_ok(self._idea_hits, voter, IDEA_RATE_LIMIT):
                return False, "Too many ideas submitted - try again later.", 429
            title = clean_text(title, 80)
            desc = clean_text(desc, 500)
            if len(title) < 3:
                return False, "Title needs to be at least 3 characters.", 400
            d = self._load()
            if len(d["ideas"]) >= COMMUNITY_MAX_IDEAS:
                return False, "The idea box is full for now - thanks for the enthusiasm.", 400
            tk = name_key(title)
            for i in d["ideas"]:
                if name_key(i["title"]) == tk:
                    return False, "That idea (or one very like it) is already on the list.", 400
            new_id = (max((i["id"] for i in d["ideas"]), default=0)) + 1
            d["ideas"].append({"id": new_id, "title": title, "desc": desc,
                                "added_at": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                                "hidden": False})
            self._save(d)
            return True, new_id, 200

    def vote(self, key, voter, valid_keys):
        with self.lock:
            if key not in valid_keys:
                return False, "That idea isn't on the page.", 400, 0
            if not self._rate_ok(self._vote_hits, voter, VOTE_RATE_LIMIT):
                d = self._load()
                return False, "Too many votes - try again later.", 429, len(d["votes"].get(key, []))
            d = self._load()
            lst = d["votes"].setdefault(key, [])
            if voter not in lst:
                lst.append(voter)
                self._save(d)
            return True, None, 200, len(lst)

    def set_hidden(self, idea_id, hidden):
        with self.lock:
            d = self._load()
            for i in d["ideas"]:
                if i["id"] == idea_id:
                    i["hidden"] = hidden
                    self._save(d)
                    return True
            return False

    def full_dump(self):
        with self.lock:
            d = self._load()
            return {"ideas": d["ideas"], "votes": {k: len(v) for k, v in d["votes"].items()}}


COMMUNITY = CommunityStore(COMMUNITY_FILE)


def client_ip(handler):
    xff = handler.headers.get("X-Forwarded-For")
    if xff:
        parts = [p.strip() for p in xff.split(",") if p.strip()]
        if parts:
            return parts[-1]
    return handler.client_address[0]


def mods_data():
    """Everything /mods and /api/mods need, computed live with a 5-minute cache on the remote fetches only."""
    servers = []
    deployed_keys = set()
    for key, label, port, dirpath in mod_servers():
        mods = deployed_mods(dirpath)
        for m in mods:
            deployed_keys.add(name_key(m["name"]))
        servers.append({"key": key, "label": label, "port": port,
                         "enabled": [m for m in mods if m["enabled"]],
                         "disabled": [m for m in mods if not m["enabled"]]})

    st_state, st_val = _cached("status_md", lambda: parse_status_md(fetch_text(STATUS_MD_URL)))
    status_rows = st_val if st_state == "ok" else []
    status_error = None if st_state == "ok" else st_val
    status_keys = {name_key(r["name"]) for r in status_rows}
    ready = [r for r in status_rows if r["status"] == "READY"]
    not_ready = [r for r in status_rows if r["status"] != "READY"]
    built_not_deployed = [r for r in ready if name_key(r["name"]) not in deployed_keys]

    id_state, id_val = _cached("ideas_md", lambda: parse_ideas_md(fetch_text(IDEAS_MD_URL)))
    idea_rows = id_val if id_state == "ok" else []
    idea_error = None if id_state == "ok" else id_val
    known = status_keys | deployed_keys
    # some ideas carry alternative names ("WhereIsEveryone / PlayerFinder"): built if any of them was built
    ideas_not_built = [i for i in idea_rows
                        if not any(name_key(n) in known for n in str(i["name"]).split("/") if n.strip())]

    hp_state, hp_val = _cached("hopper_ideas", fetch_hopper_ideas)
    hopper_error = None if hp_state == "ok" else hp_val
    hopper_rows = hp_val if (hp_state == "ok" and hp_val) else []

    for i in ideas_not_built:
        i["key"] = f"idea:{name_key(i['name'])}"
    for i in hopper_rows:
        i["key"] = f"hopper:{i['id']}"
    community_rows = COMMUNITY.ideas()
    for i in community_rows:
        i["key"] = f"community:{i['id']}"

    with COMMUNITY.lock:
        votes = COMMUNITY._load()["votes"]
    for i in ideas_not_built:
        i["votes"] = len(votes.get(i["key"], []))
    for i in hopper_rows:
        i["votes"] = len(votes.get(i["key"], []))
    ideas_not_built.sort(key=lambda i: -i["votes"])
    hopper_rows.sort(key=lambda i: -i["votes"])
    community_rows.sort(key=lambda i: -i["votes"])

    valid_keys = {i["key"] for i in ideas_not_built} | {i["key"] for i in hopper_rows} | \
                 {i["key"] for i in community_rows}

    return {
        "generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "servers": servers,
        "built_not_deployed": built_not_deployed,
        "built_not_deployed_error": status_error,
        "in_progress": not_ready,
        "in_progress_error": status_error,
        "ideas_not_built": ideas_not_built,
        "ideas_error": idea_error,
        "hopper_ideas": hopper_rows,
        "hopper_error": hopper_error,
        "community_ideas": community_rows,
        "valid_vote_keys": sorted(valid_keys),
    }


def _esc(s):
    return html.escape(str(s), quote=True)


def _mod_rows_html(items, empty_note, cols):
    """cols: list of (key, css_class_or_None) picking fields out of each dict item, joined into <td>s."""
    if not items:
        return f'<div class="muted">{_esc(empty_note)}</div>'
    out = ['<div class="tablewrap"><table><tbody>']
    for it in items:
        cells = "".join(("<td class='%s'>" % c if c else "<td>") + _esc(it.get(k, "")) + "</td>" for k, c in cols)
        search = _esc(" ".join(str(it.get(k, "")) for k, _ in cols).lower())
        out.append(f'<tr data-s="{search}">{cells}</tr>')
    out.append("</tbody></table></div>")
    return "".join(out)


def _vote_rows_html(items, empty_note, cols):
    """Like _mod_rows_html but prepends a vote button + count column keyed off item['key']/item['votes']."""
    if not items:
        return f'<div class="muted">{_esc(empty_note)}</div>'
    out = ['<div class="tablewrap"><table><tbody>']
    for it in items:
        cells = "".join(("<td class='%s'>" % c if c else "<td>") + _esc(it.get(k, "")) + "</td>" for k, c in cols)
        search = _esc(" ".join(str(it.get(k, "")) for k, _ in cols).lower())
        key = _esc(it["key"])
        votes = int(it.get("votes", 0))
        vote_cell = (f'<td class="votecell" data-key="{key}">'
                     f'<button class="vbtn" type="button" data-key="{key}" onclick="acbVote(this)">&#9650;</button> '
                     f'<span class="vcount">{votes}</span></td>')
        out.append(f'<tr data-s="{search}">{vote_cell}{cells}</tr>')
    out.append("</tbody></table></div>")
    return "".join(out)


def _community_form_html():
    return '''
    <form id="ideaform" class="ideaform" onsubmit="return acbSubmitIdea(event)">
      <p class="note">Suggestions from players. They are ideas only - nothing here is scheduled to be built;
      Tom picks what gets made.</p>
      <input type="text" name="title" id="idea_title" placeholder="Idea title" maxlength="80" required>
      <textarea name="desc" id="idea_desc" placeholder="A sentence or two (optional)" maxlength="500" rows="2"></textarea>
      <input type="text" name="website" id="idea_website" class="hp" tabindex="-1" autocomplete="off">
      <button type="submit">Add idea</button>
      <span id="idea_msg" class="muted"></span>
    </form>'''


def render_mods_html(d):
    servers_html = []
    for s in d["servers"]:
        servers_html.append(f'''
    <div class="card">
      <h2>{_esc(s["label"])} &middot; port {s["port"]}</h2>
      <div class="title"><b>Enabled ({len(s["enabled"])})</b></div>
      {_mod_rows_html(s["enabled"], "none enabled", [("name", None)])}
      <div class="title" style="margin-top:10px"><b>Disabled ({len(s["disabled"])})</b></div>
      {_mod_rows_html(s["disabled"], "none disabled", [("name", None)])}
    </div>''')

    built_html = (f'<div class="muted">couldn\'t load right now ({_esc(d["built_not_deployed_error"])})</div>'
                  if d["built_not_deployed_error"] else
                  _mod_rows_html(d["built_not_deployed"], "nothing built and waiting", [("name", None), ("summary", "wide")]))
    progress_html = (f'<div class="muted">couldn\'t load right now ({_esc(d["in_progress_error"])})</div>'
                      if d["in_progress_error"] else
                      _mod_rows_html(d["in_progress"], "nothing in progress",
                                     [("name", None), ("status", None), ("summary", "wide")]))
    ideas_html = (f'<div class="muted">couldn\'t load right now ({_esc(d["ideas_error"])})</div>'
                  if d["ideas_error"] else
                  _vote_rows_html(d["ideas_not_built"], "no un-built ideas",
                                  [("num", None), ("name", None), ("feas", None), ("desc", "wide")]))
    hopper_html = (f'<div class="muted">couldn\'t load right now ({_esc(d["hopper_error"])})</div>'
                   if d["hopper_error"] else
                   _vote_rows_html(d["hopper_ideas"], "nothing from the hopper right now",
                                   [("id", None), ("status", None), ("text", "wide")]))
    community_html = _vote_rows_html(d["community_ideas"], "no player ideas yet - be the first",
                                      [("title", None), ("desc", "wide")])

    enabled_count = sum(len(s["enabled"]) for s in d["servers"])
    disabled_count = sum(len(s["disabled"]) for s in d["servers"])
    built_count = len(d["built_not_deployed"]) if not d["built_not_deployed_error"] else 0
    progress_count = len(d["in_progress"]) if not d["in_progress_error"] else 0
    ideas_count = (len(d["ideas_not_built"]) if not d["ideas_error"] else 0) + \
                  (len(d["hopper_ideas"]) if not d["hopper_error"] else 0) + len(d["community_ideas"])

    return f'''<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta http-equiv="refresh" content="300">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ACBuilds Mods</title>
<style>
  :root {{
    --bg: #f4f5f7; --panel: #ffffff; --ink: #1c2330; --muted: #667085; --line: #e3e6ec;
    --accent: #2b5fd9; --shadow: 0 1px 2px rgba(16,24,40,.06), 0 1px 3px rgba(16,24,40,.08);
  }}
  @media (prefers-color-scheme: dark) {{
    :root {{ --bg: #10141b; --panel: #171c26; --ink: #e7eaf0; --muted: #98a2b3; --line: #262d3b; --accent: #7aa2ff; --shadow: none; }}
  }}
  * {{ box-sizing: border-box; }}
  body {{ margin: 0; background: var(--bg); color: var(--ink); font: 14px/1.45 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }}
  .wrap {{ max-width: 1000px; margin: 0 auto; padding: 20px 16px 48px; }}
  header {{ display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }}
  h1 {{ font-size: 20px; margin: 0; letter-spacing: .2px; }}
  .sub {{ color: var(--muted); font-size: 12px; }}
  .sub a {{ color: var(--accent); }}
  .grid {{ display: grid; gap: 14px; }}
  .g2 {{ grid-template-columns: repeat(2, 1fr); }}
  @media (max-width: 780px) {{ .g2 {{ grid-template-columns: 1fr; }} }}
  .card {{ background: var(--panel); border: 1px solid var(--line); border-radius: 10px; padding: 14px 16px; box-shadow: var(--shadow); min-width: 0; margin-bottom: 14px; }}
  .card h2 {{ font-size: 13px; text-transform: uppercase; letter-spacing: .6px; color: var(--muted); margin: 0 0 4px; font-weight: 600; }}
  .card h2 .n {{ color: var(--accent); }}
  .note {{ color: var(--muted); font-size: 12px; margin: 0 0 10px; }}
  .title {{ margin-bottom: 4px; }}
  .tablewrap {{ overflow-x: auto; }}
  table {{ border-collapse: collapse; width: 100%; }}
  td {{ text-align: left; padding: 5px 8px; border-bottom: 1px dashed var(--line); vertical-align: top; white-space: nowrap; font-size: 13px; }}
  td.wide {{ white-space: normal; }}
  tr:hover td {{ background: color-mix(in srgb, var(--accent) 6%, transparent); }}
  .muted {{ color: var(--muted); font-size: 12px; }}
  input[type=search] {{ background: var(--bg); color: var(--ink); border: 1px solid var(--line); border-radius: 8px; padding: 7px 10px; font: inherit; width: 100%; margin-bottom: 16px; }}
  footer {{ margin-top: 18px; color: var(--muted); font-size: 12px; }}
  tr.hide {{ display: none; }}
  td.votecell {{ white-space: nowrap; width: 1%; }}
  .vbtn {{ background: var(--bg); color: var(--accent); border: 1px solid var(--line); border-radius: 6px;
           padding: 2px 8px; font: inherit; cursor: pointer; }}
  .vbtn:disabled {{ opacity: .5; cursor: default; }}
  .vcount {{ display: inline-block; min-width: 1.4em; text-align: right; }}
  .ideaform {{ display: flex; flex-wrap: wrap; gap: 8px; align-items: flex-start; margin-bottom: 12px; }}
  .ideaform input[type=text], .ideaform textarea {{ background: var(--bg); color: var(--ink); border: 1px solid var(--line);
           border-radius: 8px; padding: 7px 10px; font: inherit; }}
  .ideaform input#idea_title {{ flex: 1 1 220px; }}
  .ideaform textarea {{ flex: 2 1 320px; resize: vertical; }}
  .ideaform button {{ background: var(--accent); color: #fff; border: none; border-radius: 8px; padding: 8px 14px; font: inherit; cursor: pointer; }}
  .ideaform .hp {{ position: absolute; left: -9999px; width: 1px; height: 1px; opacity: 0; }}
</style>
</head>
<body>
<div class="wrap">
  <header>
    <div>
      <h1>ACBuilds mods</h1>
      <div class="sub"><a href="/">&larr; server status</a></div>
    </div>
    <div class="sub">Updated {_esc(d["generated"])} UTC &middot; refreshes every 5 minutes</div>
  </header>

  <input type="search" id="q" placeholder="Filter by mod name or text&hellip;">

  <div class="grid g2">{"".join(servers_html)}</div>

  <div class="card">
    <h2>Built, not deployed <span class="n">({built_count})</span></h2>
    <p class="note">Compiled and marked READY in the mods repo, but its folder isn't on either game server yet.</p>
    {built_html}
  </div>

  <div class="card">
    <h2>In progress / not ready <span class="n">({progress_count})</span></h2>
    <p class="note">Started but not yet READY - work in progress, or needs an in-game test before it ships.</p>
    {progress_html}
  </div>

  <div class="card">
    <h2>Community ideas <span class="n">({len(d["community_ideas"])})</span></h2>
    {_community_form_html()}
    {community_html}
  </div>

  <div class="card">
    <h2>Ideas, not built yet <span class="n">({ideas_count})</span></h2>
    <p class="note">Researched ideas with no code started, plus open hopper items tagged for ACBuilds.</p>
    {ideas_html}
    <div class="title" style="margin-top:14px"><b>From the hopper</b></div>
    {hopper_html}
  </div>

  <footer>Deployed/enabled state comes from each server's own mod folder; built and idea state come from the mods-proposed
  repo and the hopper. Names and short descriptions only - no accounts, players, IPs or file paths.</footer>
</div>
<script>
  var q = document.getElementById('q');
  q.addEventListener('input', function () {{
    var v = q.value.trim().toLowerCase();
    document.querySelectorAll('tr[data-s]').forEach(function (tr) {{
      tr.classList.toggle('hide', v && tr.getAttribute('data-s').indexOf(v) === -1);
    }});
  }});

  function acbVoted() {{
    try {{ return JSON.parse(localStorage.getItem('acb_voted') || '{{}}'); }} catch (e) {{ return {{}}; }}
  }}
  function acbMarkVoted(key) {{
    try {{ var v = acbVoted(); v[key] = true; localStorage.setItem('acb_voted', JSON.stringify(v)); }} catch (e) {{}}
  }}
  (function () {{
    var voted = acbVoted();
    document.querySelectorAll('.vbtn').forEach(function (b) {{
      if (voted[b.getAttribute('data-key')]) {{ b.disabled = true; }}
    }});
  }})();

  function acbVote(btn) {{
    var key = btn.getAttribute('data-key');
    btn.disabled = true;
    fetch('/api/mods/vote', {{
      method: 'POST', headers: {{'Content-Type': 'application/json'}}, body: JSON.stringify({{key: key}})
    }}).then(function (r) {{ return r.json(); }}).then(function (j) {{
      if (j.ok) {{
        acbMarkVoted(key);
        var cell = btn.closest('.votecell');
        if (cell) {{ var c = cell.querySelector('.vcount'); if (c) {{ c.textContent = j.votes; }} }}
      }} else {{
        btn.disabled = false;
      }}
    }}).catch(function () {{ btn.disabled = false; }});
  }}

  function acbSubmitIdea(ev) {{
    ev.preventDefault();
    var msg = document.getElementById('idea_msg');
    var title = document.getElementById('idea_title').value;
    var desc = document.getElementById('idea_desc').value;
    var website = document.getElementById('idea_website').value;
    msg.textContent = 'Adding...';
    fetch('/api/mods/idea', {{
      method: 'POST', headers: {{'Content-Type': 'application/json'}},
      body: JSON.stringify({{title: title, desc: desc, website: website}})
    }}).then(function (r) {{ return r.json(); }}).then(function (j) {{
      if (j.ok) {{
        msg.textContent = 'Added - thanks!';
        setTimeout(function () {{ location.reload(); }}, 700);
      }} else {{
        msg.textContent = j.error || 'Could not add that idea.';
      }}
    }}).catch(function () {{ msg.textContent = 'Could not reach the server.'; }});
    return false;
  }}
</script>
</body>
</html>'''


# ------------------------------------------------------------------ server
class State:
    def __init__(self, places, interval):
        self.places, self.interval = places, interval
        self.data, self.lock = {"generated": None, "errors": ["starting"], "servers": [], "players": []}, threading.Lock()

    def loop(self):
        while True:
            try:
                d = collect(self.places)
            except Exception as e:
                d = {"generated": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "errors": [f"collector crashed: {e}"], "servers": [], "players": []}
            with self.lock:
                self.data = d
            time.sleep(self.interval)


def make_handler(state, index_path):
    class H(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def _send(self, code, body, ctype):
            b = body if isinstance(body, bytes) else body.encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(b)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(b)

        def do_GET(self):
            path = self.path.split("?")[0]
            if path in ("/", "/index.html"):
                self._send(200, index_path.read_bytes(), "text/html; charset=utf-8")
            elif path == "/api/status":
                with state.lock:
                    self._send(200, json.dumps(state.data), "application/json")
            elif path == "/mods":
                try:
                    self._send(200, render_mods_html(mods_data()), "text/html; charset=utf-8")
                except Exception as e:
                    self._send(200, f"<pre>mods page failed: {html.escape(str(e))}</pre>", "text/html; charset=utf-8")
            elif path == "/api/mods":
                try:
                    self._send(200, json.dumps(mods_data()), "application/json")
                except Exception as e:
                    self._send(200, json.dumps({"error": str(e)}), "application/json")
            elif path == "/healthz":
                self._send(200, "ok", "text/plain")
            elif path == "/api/health":
                try:
                    self._send(200, json.dumps(health()), "application/json")
                except Exception as e:
                    self._send(200, json.dumps({"overall_healthy": False, "error": str(e),
                                                  "checked_at": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}),
                               "application/json")
            elif path == "/api/admin/community":
                try:
                    self._send(200, json.dumps(COMMUNITY.full_dump()), "application/json")
                except Exception as e:
                    self._send(200, json.dumps({"error": str(e)}), "application/json")
            else:
                self._send(404, "not found", "text/plain")

        def _read_json_body(self, max_bytes=4096):
            """Returns (obj, error_message). error_message is None on success."""
            try:
                length = int(self.headers.get("Content-Length", "0"))
            except ValueError:
                return None, "bad request"
            if length <= 0:
                return None, "empty body"
            if length > max_bytes:
                return None, "request too large"
            raw = self.rfile.read(length)
            try:
                return json.loads(raw.decode("utf-8")), None
            except Exception:
                return None, "bad json"

        def _json(self, code, obj):
            self._send(code, json.dumps(obj), "application/json")

        def do_POST(self):
            path = self.path.split("?")[0]
            if path == "/api/mods/vote":
                obj, err = self._read_json_body()
                if err:
                    self._json(400, {"ok": False, "error": err})
                    return
                key = str((obj or {}).get("key", ""))[:200]
                ip = client_ip(self)
                voter = COMMUNITY.voter_hash(ip)
                try:
                    d = mods_data()
                except Exception as e:
                    self._json(500, {"ok": False, "error": str(e)})
                    return
                ok, msg, code, votes = COMMUNITY.vote(key, voter, set(d["valid_vote_keys"]))
                if ok:
                    self._json(200, {"ok": True, "votes": votes})
                else:
                    self._json(code, {"ok": False, "error": msg, "votes": votes})
            elif path == "/api/mods/idea":
                obj, err = self._read_json_body()
                if err:
                    self._json(400, {"ok": False, "error": err})
                    return
                obj = obj or {}
                if str(obj.get("website", "")).strip():
                    # honeypot tripped: pretend success, store nothing
                    self._json(200, {"ok": True, "id": None})
                    return
                title = obj.get("title", "")
                desc = obj.get("desc", "")
                if len(clean_text(title, 200)) < 3 or len(clean_text(title, 200)) > 80 or len(str(desc)) > 2000:
                    self._json(400, {"ok": False, "error": "Title needs to be 3-80 characters."})
                    return
                ip = client_ip(self)
                voter = COMMUNITY.voter_hash(ip)
                ok, res, code = COMMUNITY.add_idea(title, desc, voter)
                if ok:
                    self._json(200, {"ok": True, "id": res})
                else:
                    self._json(code, {"ok": False, "error": res})
            elif path == "/api/admin/community/hide":
                self._admin_set_hidden(True)
            elif path == "/api/admin/community/unhide":
                self._admin_set_hidden(False)
            else:
                self._send(404, "not found", "text/plain")

        def _admin_set_hidden(self, hidden):
            obj, err = self._read_json_body()
            if err:
                self._json(400, {"ok": False, "error": err})
                return
            try:
                idea_id = int((obj or {}).get("id"))
            except (TypeError, ValueError):
                self._json(400, {"ok": False, "error": "bad id"})
                return
            ok = COMMUNITY.set_hidden(idea_id, hidden)
            self._json(200 if ok else 404, {"ok": ok})

    return H


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8618)
    ap.add_argument("--data", default=os.environ.get("ACB_STATUS_DATA", str(HERE / "data")))
    ap.add_argument("--interval", type=int, default=15)
    ap.add_argument("--once", action="store_true")
    a = ap.parse_args()
    places = Places(a.data)
    print("locations:", "; ".join(places.loaded) or "NONE (put locations.xml / cod_locations.xml in " + a.data + ")", flush=True)
    if a.once:
        print(json.dumps(collect(places), indent=1))
        return
    st = State(places, a.interval)
    threading.Thread(target=st.loop, daemon=True).start()
    srv = ThreadingHTTPServer(("127.0.0.1", a.port), make_handler(st, HERE / "index.html"))
    print(f"status page on http://127.0.0.1:{a.port}/", flush=True)
    srv.serve_forever()


if __name__ == "__main__":
    main()
