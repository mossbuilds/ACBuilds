using System.Text.Json;
using ACE.Database;
using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AgentActions;

// No Harmony patches - this mod only calls into ACE (spawn/speak/broadcast/reposition), same shape as EventClock.
// The [HarmonyPatch] attribute is kept only because BasicPatch<T> expects one; there are no [HarmonyPatch] methods below.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new(); // guards inbox rewrite and log append together
    private static Timer? timer;

    // Rolling hour window of accepted-broadcast timestamps (unix seconds), for MaxBroadcastsPerHour.
    private static readonly List<double> broadcastTimes = new();

    private static double Now() => ACE.Common.Time.GetUnixTime();

    private static string Clean(string s, int max)
    {
        var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return t.Length > max ? t[..max] : t;
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var period = TimeSpan.FromSeconds(Math.Max(1, Cfg.PollSeconds));
            timer = new Timer(_ =>
            {
                try { PollInbox(); }
                catch (Exception e) { ModManager.Log($"[AgentActions] poll: {e.Message}"); }
            }, null, period, period);
        }
        ModManager.Log($"[AgentActions] ready, enabled={Cfg.Enabled}, allowed spawn wcids={Cfg.AllowedSpawnWcids.Count}");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    // --- log -------------------------------------------------------------------------------------------------

    private static void AppendLog(object entry)
    {
        var cfg = Cfg;
        if (cfg == null) return;
        try
        {
            var line = JsonSerializer.Serialize(entry);
            var dir = Path.GetDirectoryName(cfg.LogFile);
            lock (gate)
            {
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
            }
        }
        catch (Exception e) { ModManager.Log($"[AgentActions] log write: {e.Message}"); }
    }

    private static void Accepted(string type, object raw, object result) =>
        AppendLog(new { time = Now(), status = "accepted", type, request = raw, result });

    private static void Refused(string type, object raw, string reason)
    {
        AppendLog(new { time = Now(), status = "refused", type, request = raw, reason });
        ModManager.Log($"[AgentActions] refused {type}: {reason}");
    }

    // --- shared lookups ----------------------------------------------------------------------------------------

    // Same enumeration AgentDialogue/HotspotAlert use to find a currently-loaded instance of a wcid.
    private static WorldObject? FindLoadedNpc(uint wcid)
    {
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            if (lb == null) continue;
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                if (wo.WeenieClassId == wcid) return wo;
        }
        return null;
    }

    private static bool IsForbiddenWeenieType(ACE.Entity.Enum.WeenieType t) =>
        t == ACE.Entity.Enum.WeenieType.Admin || t == ACE.Entity.Enum.WeenieType.Sentinel || t == ACE.Entity.Enum.WeenieType.Undef;

    // --- speak: same Say-broadcast approach as AgentDialogue's outbox -----------------------------------------

    private static void Speak(WorldObject npc, string text)
    {
        var name = npc.CreatureType == CreatureType.Olthoi ? npc.Name + "&" : npc.Name;
        npc.EnqueueBroadcast(new GameMessageHearSpeech(text, name, npc.Guid.Full, ChatMessageType.Emote), WorldObject.LocalBroadcastRange);
    }

    private static void HandleSpeak(JsonElement root, object raw)
    {
        if (!root.TryGetProperty("npc_wcid", out var wEl) || !wEl.TryGetUInt32(out var wcid) || wcid == 0)
        { Refused("speak", raw, "missing/invalid npc_wcid"); return; }

        var text = root.TryGetProperty("text", out var tEl) ? Clean(tEl.GetString() ?? "", Cfg!.MaxTextLength) : "";
        if (text.Length == 0) { Refused("speak", raw, "empty text after cleaning"); return; }

        var npc = FindLoadedNpc(wcid);
        if (npc == null) { Refused("speak", raw, $"no loaded instance of wcid {wcid}"); return; }

        new ActionChain(WorldManager.ActionQueue, () => Speak(npc, text)).EnqueueChain();
        Accepted("speak", raw, new { npc_wcid = wcid, text });
    }

    // --- broadcast: server-wide, rate-limited -------------------------------------------------------------------

    private static bool BroadcastAllowed(Settings cfg)
    {
        var now = Now();
        broadcastTimes.RemoveAll(t => now - t > 3600);
        return broadcastTimes.Count < Math.Max(0, cfg.MaxBroadcastsPerHour);
    }

    private static void HandleBroadcast(JsonElement root, object raw)
    {
        var cfg = Cfg!;
        var text = root.TryGetProperty("text", out var tEl) ? Clean(tEl.GetString() ?? "", cfg.MaxTextLength) : "";
        if (text.Length == 0) { Refused("broadcast", raw, "empty text after cleaning"); return; }

        lock (gate)
        {
            if (!BroadcastAllowed(cfg)) { Refused("broadcast", raw, $"rate limit: {cfg.MaxBroadcastsPerHour}/hour reached"); return; }
            broadcastTimes.Add(Now());
        }

        // PlayerManager.BroadcastToAll(GameMessage) verified in PlayerManager.cs; same call AnnounceEvents/WorldBoss use.
        new ActionChain(WorldManager.ActionQueue, () =>
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(text, ChatMessageType.WorldBroadcast))).EnqueueChain();
        Accepted("broadcast", raw, new { text });
    }

    // --- spawn: allowlisted wcid only, next to an already-loaded NPC -------------------------------------------

    private static void HandleSpawn(JsonElement root, object raw)
    {
        var cfg = Cfg!;
        if (!root.TryGetProperty("wcid", out var wEl) || !wEl.TryGetUInt32(out var wcid) || wcid == 0)
        { Refused("spawn", raw, "missing/invalid wcid"); return; }
        if (!root.TryGetProperty("near_npc_wcid", out var nEl) || !nEl.TryGetUInt32(out var nearWcid) || nearWcid == 0)
        { Refused("spawn", raw, "missing/invalid near_npc_wcid"); return; }

        if (!cfg.AllowedSpawnWcids.Contains(wcid))
        { Refused("spawn", raw, $"wcid {wcid} is not on AllowedSpawnWcids"); return; }

        var weenie = DatabaseManager.World.GetCachedWeenie(wcid);
        if (weenie == null) { Refused("spawn", raw, $"wcid {wcid} not found in world database"); return; }
        // ACE.Entity.Models.Weenie.WeenieType is already the typed enum (not a raw int cast). Defense in
        // depth: even an admin-misconfigured allowlist entry can never spawn one of these.
        var weenieType = weenie.WeenieType;
        if (IsForbiddenWeenieType(weenieType)) { Refused("spawn", raw, $"wcid {wcid} has forbidden WeenieType {weenieType}"); return; }

        var nearNpc = FindLoadedNpc(nearWcid);
        if (nearNpc == null) { Refused("spawn", raw, $"no loaded instance of near_npc_wcid {nearWcid}"); return; }

        new ActionChain(WorldManager.ActionQueue, () =>
        {
            try
            {
                // Same pattern as RaiseSkeleton.SummonFeral / WorldBoss.Start: CreateNewWorldObject, InFrontOf, EnterWorld.
                var obj = WorldObjectFactory.CreateNewWorldObject(weenie);
                if (obj == null) { Refused("spawn", raw, "CreateNewWorldObject returned null"); return; }
                if (IsForbiddenWeenieType(obj.WeenieType)) { obj.Destroy(); Refused("spawn", raw, $"spawned object has forbidden WeenieType {obj.WeenieType}"); return; }

                obj.Location = nearNpc.Location.InFrontOf(2.5f);
                obj.Location.LandblockId = new LandblockId(obj.Location.GetCell());
                if (!obj.EnterWorld())
                {
                    obj.Destroy();
                    Refused("spawn", raw, "EnterWorld failed (blocked position?)");
                    return;
                }
                Accepted("spawn", raw, new { wcid, near_npc_wcid = nearWcid, guid = obj.Guid.Full, location = obj.Location.ToLOCString() });
            }
            catch (Exception e) { Refused("spawn", raw, $"exception: {e.Message}"); }
        }).EnqueueChain();
    }

    // --- teleport_npc: reposition an already-loaded, non-player instance ----------------------------------------

    private static bool FiniteAndInRange(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 1_000_000f;

    private static void HandleTeleportNpc(JsonElement root, object raw)
    {
        if (!root.TryGetProperty("npc_wcid", out var wEl) || !wEl.TryGetUInt32(out var wcid) || wcid == 0)
        { Refused("teleport_npc", raw, "missing/invalid npc_wcid"); return; }
        if (!root.TryGetProperty("cell", out var cEl))
        { Refused("teleport_npc", raw, "missing cell"); return; }

        uint cell;
        try
        {
            cell = cEl.ValueKind == JsonValueKind.String
                ? Convert.ToUInt32((cEl.GetString() ?? "0").Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16)
                : cEl.GetUInt32();
        }
        catch { Refused("teleport_npc", raw, "cell is not a valid uint or hex string"); return; }
        if (cell == 0) { Refused("teleport_npc", raw, "cell is 0"); return; }

        if (!root.TryGetProperty("x", out var xEl) || !root.TryGetProperty("y", out var yEl) || !root.TryGetProperty("z", out var zEl)
            || !xEl.TryGetSingle(out var x) || !yEl.TryGetSingle(out var y) || !zEl.TryGetSingle(out var z))
        { Refused("teleport_npc", raw, "missing/invalid x/y/z"); return; }

        // Loose sanity check only - this cannot verify walkability/indoor-outdoor validity from here.
        if (!FiniteAndInRange(x) || !FiniteAndInRange(y) || !FiniteAndInRange(z))
        { Refused("teleport_npc", raw, "x/y/z out of sane range"); return; }

        var npc = FindLoadedNpc(wcid);
        if (npc == null) { Refused("teleport_npc", raw, $"no loaded instance of wcid {wcid}"); return; }
        // Hard refusal regardless of wcid lookup result: never move a Player-derived object.
        if (npc is Player) { Refused("teleport_npc", raw, "resolved object is a Player - refused"); return; }

        new ActionChain(WorldManager.ActionQueue, () =>
        {
            try
            {
                if (npc.IsDestroyed) { Refused("teleport_npc", raw, "npc was destroyed before the chain ran"); return; }
                npc.Location = new Position(cell, x, y, z, 0, 0, 0, 1);
                // LandblockManager.RelocateObjectForPhysics (verified in LandblockManager.cs): "should only be called
                // from physics/worldmanager - not player", which fits a non-player NPC reposition. Removes from the
                // old landblock and adds to the new one; it does not re-run EnterWorld or generator hooks.
                LandblockManager.RelocateObjectForPhysics(npc, false);
                Accepted("teleport_npc", raw, new { npc_wcid = wcid, location = npc.Location.ToLOCString() });
            }
            catch (Exception e) { Refused("teleport_npc", raw, $"exception: {e.Message}"); }
        }).EnqueueChain();
    }

    // --- poll --------------------------------------------------------------------------------------------------

    private static void PollInbox()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;

        List<string> lines;
        lock (gate)
            lines = File.Exists(cfg.InboxFile) ? File.ReadAllLines(cfg.InboxFile).ToList() : new List<string>();
        if (lines.Count == 0) return;

        // Every line is consumed this poll - accepted requests are queued on an ActionChain (async), refused/malformed
        // ones are logged immediately. Either way the line never stays in inbox.jsonl past this poll, per the plan.
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(raw); }
            catch (Exception e)
            {
                AppendLog(new { time = Now(), status = "refused", type = (string?)null, request = raw, reason = $"malformed JSON: {e.Message}" });
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "" : "";
                object rawObj = raw; // logged verbatim as the original line
                switch (type)
                {
                    case "speak": HandleSpeak(root, rawObj); break;
                    case "broadcast": HandleBroadcast(root, rawObj); break;
                    case "spawn": HandleSpawn(root, rawObj); break;
                    case "teleport_npc": HandleTeleportNpc(root, rawObj); break;
                    default: Refused(type.Length == 0 ? "(missing)" : type, rawObj, "unknown or missing type - only speak/broadcast/spawn/teleport_npc are handled"); break;
                }
            }
        }

        lock (gate)
            File.WriteAllLines(cfg.InboxFile, Array.Empty<string>());
    }
}
