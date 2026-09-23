#!/usr/bin/env python3
"""ACBuilds status service: game-server health, database stats, players and where they are.

Runs on the host next to the Docker containers (Python standard library only), serves a page and a JSON API on 127.0.0.1
(put a reverse proxy with TLS + auth in front of it). Read-only: it only runs SELECTs and `docker inspect/logs/exec`.

  python3 acb_status.py [--port 8618] [--data DIR]        DIR holds locations.xml and cod_locations.xml
  python3 acb_status.py --once                            print the JSON once and exit (debug)
"""
import argparse, datetime, html, json, math, os, re, subprocess, sys, threading, time
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
        if "World is now open" in line:
            res["world_open"] = True
        elif "Current Server Binary:" in line:
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
            elif path == "/healthz":
                self._send(200, "ok", "text/plain")
            elif path == "/api/health":
                try:
                    self._send(200, json.dumps(health()), "application/json")
                except Exception as e:
                    self._send(200, json.dumps({"overall_healthy": False, "error": str(e),
                                                  "checked_at": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}),
                               "application/json")
            else:
                self._send(404, "not found", "text/plain")

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
