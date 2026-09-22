using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace GeneratorAudit;

// GeneratorProfile.cs (ACE.Server.Entity) was fetched directly and read in full for this mod (raw path:
// Source/ACE.Server/Entity/GeneratorProfile.cs - the idea's own note that this file 404'd was against a guessed
// WorldObjects/ path; it actually lives under Entity/). IsPlaceholder, IsMaxed and IsAvailable are all confirmed
// public auto-properties on GeneratorProfile itself, same access level the idea's WorldObject-level members
// (IsGenerator, GeneratorProfiles, CurrentCreate, AllProfilesMaxed, AllProfilesUnavailable) already had confirmed -
// so nothing here needs a Traverse/reflection fallback the way StuckVendorWatch's ResetTimestamp read did. This
// mod still only ever calls the four WorldObject-level members per the idea's scope; the profile-level names are
// noted here only to close out the idea's "unverified" flag, not because the mod reads them itself.
//
// No public timestamp exists anywhere in GeneratorProfile.cs or WorldObject_Generators.cs for "how long has
// AllProfilesUnavailable been true across the whole generator". GeneratorProfile.NextAvailable is public, but it's
// per-profile and forward-looking (a moment a single profile becomes available again, not a record of when the
// whole generator last had any profile available) - it does not aggregate the way AllProfilesUnavailable does, and
// there is no equivalent "AllProfilesUnavailableSince" anywhere. So, like DecayClock's DecaySeconds/Last tracking
// (PatchClass.cs, this repo), this mod keeps its own in-memory clock per generator: first-seen-stalled timestamp,
// cleared the moment the generator is no longer all-unavailable, and lost on restart (acceptable - a restart also
// resets the world's own generator state).
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object Gate = new();

    // guid -> UTC instant this generator was first observed with AllProfilesUnavailable true, at every look since.
    private static readonly Dictionary<uint, DateTime> FirstSeenStalled = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var every = TimeSpan.FromSeconds(Math.Max(30, Cfg.ScanSeconds));
            timer = new Timer(_ =>
            {
                try { Scan(); }
                catch (Exception e) { ModManager.Log($"[GeneratorAudit] {e.Message}"); }
            }, null, every, every);
        }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        lock (Gate) FirstSeenStalled.Clear();
        base.Stop();
    }

    private readonly struct Flag(uint landblockRaw, uint guid, string reason)
    {
        public readonly uint LandblockRaw = landblockRaw;
        public readonly uint Guid = guid;
        public readonly string Reason = reason;
    }

    // Walks every loaded landblock (same LandblockManager.GetLoadedLandblocks() + GetAllWorldObjectsForDiagnostics()
    // pattern already shipped in ServerPulse/HotspotAlert/StuckVendorWatch), finds every generator, and updates
    // the self-tracked stall clock for each one. A generator only gets flagged once it has been continuously
    // AllProfilesUnavailable across every look for at least ScanSeconds - never on a single snapshot, so a
    // generator just caught mid-cycle between spawns doesn't false-positive on one unlucky scan.
    private static List<Flag> ScanNow(int minStallSeconds, bool updateTracking)
    {
        var flags = new List<Flag>();
        var now = DateTime.UtcNow;
        var seen = new HashSet<uint>();

        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
            {
                if (!wo.IsGenerator) continue;

                var guid = wo.Guid.Full;
                seen.Add(guid);

                var stalled = wo.AllProfilesUnavailable;
                DateTime firstSeen;

                lock (Gate)
                {
                    if (!stalled)
                    {
                        if (updateTracking) FirstSeenStalled.Remove(guid);
                        continue;
                    }

                    if (!FirstSeenStalled.TryGetValue(guid, out firstSeen))
                    {
                        firstSeen = now;
                        if (updateTracking) FirstSeenStalled[guid] = firstSeen;
                    }
                }

                var seconds = (int)(now - firstSeen).TotalSeconds;
                if (seconds < minStallSeconds) continue;

                var maxedNote = wo.AllProfilesMaxed ? ", all profiles also maxed" : "";
                flags.Add(new Flag(lb.Id.Raw, guid,
                    $"all {wo.GeneratorProfiles?.Count ?? 0} profile(s) unavailable for at least {seconds}s (CurrentCreate={wo.CurrentCreate}{maxedNote})"));
            }
        }

        if (updateTracking)
        {
            lock (Gate)
                foreach (var k in FirstSeenStalled.Keys.Where(k => !seen.Contains(k)).ToList())
                    FirstSeenStalled.Remove(k); // generator unloaded/gone - drop its clock rather than let it grow stale
        }

        return flags;
    }

    private static void Scan()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var minStall = Math.Max(30, cfg.ScanSeconds);
        var flags = ScanNow(minStall, updateTracking: true);
        if (flags.Count == 0) return;
        foreach (var p in PlayerManager.GetAllOnline())
        {
            if (p.Session == null || p.Session.AccessLevel < AccessLevel.Sentinel) continue;
            foreach (var f in flags)
                p.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"GeneratorAudit: generator 0x{f.Guid:X8} in landblock {f.LandblockRaw >> 16:X4} - {f.Reason}",
                    ChatMessageType.Broadcast));
        }
    }

    [CommandHandler("genaudit", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
        "Lists generators whose profiles have all looked unavailable for longer than ScanSeconds, straight through every look.", "/genaudit [landblock]")]
    public static void HandleGenAudit(Session session, params string[] parameters)
    {
        void Send(string msg)
        {
            if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            else Console.WriteLine(msg);
        }

        var cfg = Cfg;
        var minStall = Math.Max(30, cfg?.ScanSeconds ?? 60);

        Send("GeneratorAudit is read-only: it never touches, resets, or regenerates a generator. A generator " +
             "legitimately idle by design (an event trigger, a one-shot boss room, a quest-gated spawn) can show " +
             "up here too - that's a false positive, not a bug; verify by hand before assuming anything is broken.");

        // Manual run also advances the tracking clock, same as the timer - so /genaudit right after enabling still
        // reports "seen since now" rather than nothing, and repeated manual runs build up real stall duration.
        var flags = ScanNow(minStall, updateTracking: true);

        ushort? filterLandblock = null;
        if (parameters.Length > 0 && ushort.TryParse(parameters[0], System.Globalization.NumberStyles.HexNumber, null, out var lbId))
            filterLandblock = lbId;

        var toShow = filterLandblock.HasValue
            ? flags.Where(f => (f.LandblockRaw >> 16) == filterLandblock.Value).ToList()
            : flags;

        if (toShow.Count == 0)
        {
            Send(filterLandblock.HasValue
                ? $"No stalled generators found in landblock {filterLandblock.Value:X4}."
                : "No stalled generators found in any loaded landblock.");
            return;
        }
        foreach (var f in toShow)
            Send($"Generator 0x{f.Guid:X8} in landblock {f.LandblockRaw >> 16:X4} - {f.Reason}");
    }
}
