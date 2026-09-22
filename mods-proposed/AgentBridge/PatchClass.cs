using System.Net;
using System.Text;
using System.Text.Json;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AgentBridge;

// AgentBridge does NOT hook any ACE event and does not need Harmony at all - it only reads state via the same
// manager calls AgentDialogue/AgentFeed already use, and appends lines to files AgentDialogue/AgentActions already
// know how to consume. The [HarmonyPatch] class attribute is kept only because BasicPatch<T> requires it (same as
// EventClock/AgentActions) - there are no [HarmonyPatch] methods below.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new(); // guards both file appends below, one lock for both files is fine at this volume
    private static HttpListener? listener;
    private static CancellationTokenSource? cts;
    private static Task? loopTask;

    private static double Now() => ACE.Common.Time.GetUnixTime();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        TryStart(Cfg);
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        TryStop();
        base.Stop();
    }

    // --- start/stop --------------------------------------------------------------------------------------------

    private static void TryStart(Settings cfg)
    {
        if (!cfg.Enabled)
        {
            ModManager.Log("[AgentBridge] disabled, not starting listener.");
            return;
        }

        // Never run unauthenticated, ever - a blank/whitespace secret is a hard refusal to start, not a soft warning.
        if (string.IsNullOrWhiteSpace(cfg.SharedSecret))
        {
            ModManager.Log("[AgentBridge] ERROR: SharedSecret is empty/whitespace - refusing to start the listener. " +
                            "Set Settings.json -> SharedSecret to a long random value and reload the mod.");
            return;
        }

        if (cfg.Port <= 0 || cfg.Port > 65535)
        {
            ModManager.Log($"[AgentBridge] ERROR: Port {cfg.Port} is out of range - refusing to start the listener.");
            return;
        }

        try
        {
            var l = new HttpListener();
            // Loopback only, ever. Never "+", "*", or a real interface/hostname - see README "READ THIS BEFORE ENABLING".
            l.Prefixes.Add($"http://127.0.0.1:{cfg.Port}/");
            l.Prefixes.Add($"http://localhost:{cfg.Port}/");
            l.Start();
            listener = l;
            cts = new CancellationTokenSource();
            loopTask = Task.Run(() => AcceptLoop(l, cts.Token));
            ModManager.Log($"[AgentBridge] listening on http://127.0.0.1:{cfg.Port}/ (loopback only).");
        }
        catch (Exception e)
        {
            // If HttpListener can't bind for any reason (port in use, http.sys unavailable, no permission), we fall
            // back to nothing running rather than a broken half-measure - never throw into ACE's mod-load path.
            ModManager.Log($"[AgentBridge] ERROR: failed to start listener, staying off: {e.Message}");
            listener = null;
        }
    }

    private static void TryStop()
    {
        try { cts?.Cancel(); } catch { /* best effort */ }
        try { listener?.Stop(); listener?.Close(); } catch (Exception e) { ModManager.Log($"[AgentBridge] stop: {e.Message}"); }
        listener = null;
        cts = null;
        loopTask = null;
        ModManager.Log("[AgentBridge] listener stopped.");
    }

    // --- accept loop --------------------------------------------------------------------------------------------
    // Runs entirely on its own background Task via HttpListener's own async GetContextAsync loop - never touches
    // ACE's world/action-queue threads directly except through the same ActionChain-free manager calls AgentDialogue
    // and AgentFeed already make from arbitrary threads (PlayerManager/LandblockManager reads are safe off the game
    // tick; this mod does not mutate any world object, only reads state and appends lines to plain files).

    private static async Task AcceptLoop(HttpListener l, CancellationToken token)
    {
        while (!token.IsCancellationRequested && l.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await l.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (token.IsCancellationRequested || !l.IsListening)
            {
                return; // listener was stopped out from under us - normal shutdown path
            }
            catch (Exception e)
            {
                ModManager.Log($"[AgentBridge] GetContextAsync: {e.Message}");
                continue;
            }

            // Never block the accept loop on one slow client - handle each request on its own task.
            _ = Task.Run(() => HandleRequestSafe(ctx));
        }
    }

    private static void HandleRequestSafe(HttpListenerContext ctx)
    {
        try
        {
            HandleRequest(ctx);
        }
        catch (Exception e)
        {
            // Never leak a stack trace to the client - generic 500 only, full detail stays in the server log.
            ModManager.Log($"[AgentBridge] unhandled request error: {e}");
            try { WriteJson(ctx.Response, 500, new { error = "internal error" }); } catch { /* client may already be gone */ }
        }
    }

    // --- request handling ---------------------------------------------------------------------------------------

    private static void HandleRequest(HttpListenerContext ctx)
    {
        var cfg = Cfg;
        var req = ctx.Request;
        var res = ctx.Response;

        if (cfg == null) { WriteJson(res, 500, new { error = "not configured" }); return; }

        // Auth first, before anything else - including before reading the body, so an unauthenticated request never
        // gets the mod to do any work at all beyond a header compare.
        var provided = req.Headers["X-Agent-Secret"];
        // Plain string equality - NOT a timing-safe comparison. Documented in README: this mod is a private-server
        // loopback convenience tool, not something meant to face any adversary who can measure response timing.
        if (string.IsNullOrEmpty(provided) || provided != cfg.SharedSecret)
        {
            WriteJson(res, 401, new { error = "missing or invalid X-Agent-Secret" });
            return;
        }

        var path = req.Url?.AbsolutePath ?? "";

        try
        {
            if (req.HttpMethod == "GET" && path == "/state")
            {
                HandleState(res, cfg);
                return;
            }

            if (req.HttpMethod == "POST" && path == "/actions")
            {
                HandleActionsPost(req, res, cfg);
                return;
            }

            if (req.HttpMethod == "POST" && path.StartsWith("/dialogue/") && path.EndsWith("/reply"))
            {
                var middle = path.Substring("/dialogue/".Length, path.Length - "/dialogue/".Length - "/reply".Length);
                if (uint.TryParse(middle, out var wcid) && wcid != 0)
                {
                    HandleDialogueReply(req, res, cfg, wcid);
                    return;
                }
                WriteJson(res, 400, new { error = "invalid wcid in path" });
                return;
            }

            WriteJson(res, 404, new { error = "unknown path" });
        }
        catch (Exception e)
        {
            ModManager.Log($"[AgentBridge] handler error on {path}: {e}");
            WriteJson(res, 500, new { error = "internal error" });
        }
    }

    // --- GET /state ---------------------------------------------------------------------------------------------
    // Deliberately minimal: character names and landblocks only, per the plan. No account names, no IPs, no stats,
    // no inventory, no exact position - just enough for an outside process to know who's around and where.

    private static WorldObject? FindLoadedNpc(uint wcid)
    {
        // Same enumeration AgentDialogue/AgentActions already use to find a currently-loaded instance of a wcid.
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            if (lb == null) continue;
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                if (wo.WeenieClassId == wcid) return wo;
        }
        return null;
    }

    private static void HandleState(HttpListenerResponse res, Settings cfg)
    {
        var players = PlayerManager.GetAllOnline()
            .Select(p => new
            {
                name = p.Name, // character name only - never account name, never IP
                landblock = $"{p.Location?.Landblock ?? 0:X4}"
            })
            .ToList();

        var watched = cfg.WatchWcids
            .Select(wcid =>
            {
                var npc = FindLoadedNpc(wcid);
                return new
                {
                    wcid,
                    loaded = npc != null,
                    landblock = npc == null ? null : $"{npc.Location?.Landblock ?? 0:X4}"
                };
            })
            .ToList();

        WriteJson(res, 200, new { time = Now(), players, watched });
    }

    // --- POST /actions -------------------------------------------------------------------------------------------
    // AgentBridge does NOT validate the action itself - it only appends the body verbatim (after confirming it is
    // well-formed JSON, so a malformed line can never land in AgentActions' inbox from this path). AgentActions'
    // own poll does the real validation (allowlists, forbidden weenie types, rate limits, player-teleport refusal)
    // exactly as it does for a line written by any other outside process. AgentActions must be enabled for a POST
    // here to ever actually take effect - see README.

    private static void HandleActionsPost(HttpListenerRequest req, HttpListenerResponse res, Settings cfg)
    {
        if (!TryReadBody(req, cfg.MaxBodyBytes, out var body, out var tooLarge))
        {
            WriteJson(res, tooLarge ? 413 : 400, new { error = tooLarge ? "request body too large" : "failed to read body" });
            return;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (Exception e)
        {
            WriteJson(res, 400, new { error = $"malformed JSON: {e.Message}" });
            return;
        }
        using (doc)
        {
            // Re-serialize (not GetRawText()) so this is genuinely one line, always: GetRawText() preserves the
            // client's original formatting verbatim, so a pretty-printed body with real newlines between tokens
            // would still split into multiple broken lines in inbox.jsonl even though the JSON itself is valid.
            // JsonSerializer.Serialize writes compact output (no embedded literal newlines outside of \n-escaped
            // string content) regardless of how the request body was formatted.
            var line = JsonSerializer.Serialize(doc.RootElement);
            AppendLine(cfg.ActionsInboxFile, line);
        }

        WriteJson(res, 202, new { status = "queued" });
    }

    // --- POST /dialogue/{wcid}/reply -----------------------------------------------------------------------------
    // Appends to AgentDialogue's outbox.jsonl in the exact shape its PatchClass.PollOutbox already parses:
    // {"npc_wcid":<uint>,"text":"..."}. AgentDialogue must be enabled and the wcid on its own WatchedWcids for this
    // to ever be spoken - AgentBridge does not check that here (it doesn't have and shouldn't need AgentDialogue's
    // settings to do its job; AgentDialogue's own poll silently refuses/drops anything off its watch-list).

    private class ReplyBody
    {
        public string? text { get; set; }
    }

    private static void HandleDialogueReply(HttpListenerRequest req, HttpListenerResponse res, Settings cfg, uint wcid)
    {
        if (!TryReadBody(req, cfg.MaxBodyBytes, out var body, out var tooLarge))
        {
            WriteJson(res, tooLarge ? 413 : 400, new { error = tooLarge ? "request body too large" : "failed to read body" });
            return;
        }

        ReplyBody? parsed;
        try { parsed = JsonSerializer.Deserialize<ReplyBody>(body); }
        catch (Exception e)
        {
            WriteJson(res, 400, new { error = $"malformed JSON: {e.Message}" });
            return;
        }

        var text = parsed?.text ?? "";
        text = new string(text.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (text.Length == 0)
        {
            WriteJson(res, 400, new { error = "text is required and must be non-empty after cleaning" });
            return;
        }
        if (text.Length > cfg.MaxReplyTextLength)
            text = text[..cfg.MaxReplyTextLength];

        var line = JsonSerializer.Serialize(new { npc_wcid = wcid, text });
        AppendLine(cfg.DialogueOutboxFile, line);

        WriteJson(res, 202, new { status = "queued" });
    }

    // --- shared file append ---------------------------------------------------------------------------------------

    private static void AppendLine(string relPath, string jsonLine)
    {
        try
        {
            var dir = Path.GetDirectoryName(relPath);
            lock (gate)
            {
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(relPath, jsonLine + Environment.NewLine);
            }
        }
        catch (Exception e) { ModManager.Log($"[AgentBridge] append to {relPath}: {e.Message}"); }
    }

    // --- shared HTTP helpers -----------------------------------------------------------------------------------

    private static bool TryReadBody(HttpListenerRequest req, int maxBytes, out string body, out bool tooLarge)
    {
        body = "";
        tooLarge = false;

        // Content-Length may be absent/wrong for chunked requests, so this is a first-line check, not the only one -
        // the read loop below also stops (and reports too-large) the moment it would exceed the cap.
        if (req.ContentLength64 > maxBytes) { tooLarge = true; return false; }

        try
        {
            using var stream = req.InputStream;
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            var total = 0;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes) { tooLarge = true; return false; }
                ms.Write(buffer, 0, read);
            }
            body = Encoding.UTF8.GetString(ms.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteJson(HttpListenerResponse res, int status, object payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            res.StatusCode = status;
            res.ContentType = "application/json";
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            ModManager.Log($"[AgentBridge] write response: {e.Message}");
        }
        finally
        {
            try { res.OutputStream.Close(); } catch { /* client may already be gone */ }
            try { res.Close(); } catch { /* client may already be gone */ }
        }
    }
}
