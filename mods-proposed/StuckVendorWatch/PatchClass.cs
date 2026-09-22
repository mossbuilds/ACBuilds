using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace StuckVendorWatch;

// Approach (a) from the idea's own decision point: reflects into Vendor's PROTECTED ResetTimestamp/ResetInterval
// fields via Harmony Traverse, the same pattern FellowshipShareToggle already ships for its private-method calls,
// guarded in try/catch. Chosen over the honest-fallback (b) because "currently closed" alone throws away the
// entire point of this idea (distinguishing a stalled reset from a vendor closed by design just needs the
// timestamp math), and because this is a pure FIELD READ (Traverse.Field(...).GetValue<T>()), not a method
// invocation that mutates state the way FellowshipShareToggle's CalculateXPSharing/UpdateAllMembers calls do -
// a rename or field removal on an ACE update can only make the read throw or return a stale default, never
// corrupt vendor state, since nothing here is ever written back. If the reflected read fails for any vendor
// (field renamed/removed in a future ACE version), that one vendor silently falls back to the OpenForBusiness-only
// check rather than aborting the whole scan.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var every = TimeSpan.FromSeconds(Math.Max(30, Cfg.ScanSeconds));
            timer = new Timer(_ =>
            {
                try { Scan(null); }
                catch (Exception e) { ModManager.Log($"[StuckVendorWatch] {e.Message}"); }
            }, null, every, every);
        }
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    private readonly struct Flag(uint landblockRaw, uint guid, string reason)
    {
        public readonly uint LandblockRaw = landblockRaw;
        public readonly uint Guid = guid;
        public readonly string Reason = reason;
    }

    // Reads a closed vendor's stall duration via reflection into the protected Vendor/WorldObject fields
    // ResetTimestamp and ResetInterval - both verified present on Vendor.cs directly, both protected (proven by
    // VendorStock's real compile attempt, this same repo). Returns null if the reflected read fails for any
    // reason, so the caller can fall back to the OpenForBusiness-only check instead of dropping the vendor.
    //
    // IMPORTANT on types: Vendor.cs's own code (`var resetInterval = ResetInterval ?? 300; ResetTimestamp =
    // Time.GetFutureUnixTime(resetInterval);`, where GetFutureUnixTime takes a `double`) shows ResetInterval is
    // nullable (the `??`) and only implicitly convertible to double, not itself a `double` field - almost
    // certainly `int?`. Reading it with `GetValue<double>()` would throw on every call (a boxed int? cannot be
    // cast straight to double), silently degrading this mod to the OpenForBusiness-only fallback on every single
    // vendor - the exact case this mod exists to do better than that fallback. Read it as `int?` and convert.
    // ResetTimestamp is a genuine `double` (it's assigned directly from `Time.GetFutureUnixTime`'s double return).
    private static double? SecondsPastDueReset(Vendor v)
    {
        try
        {
            var t = Traverse.Create(v);
            var resetTimestamp = t.Field("ResetTimestamp").GetValue<double>();
            var resetInterval = (double)(t.Field("ResetInterval").GetValue<int?>() ?? 0);
            if (resetInterval <= 0) return null; // no meaningful cycle to have stalled
            var dueAt = resetTimestamp + resetInterval;
            var nowUnix = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var overdue = nowUnix - dueAt;
            return overdue > 0 ? overdue : null; // not yet due, or due in the future - not stuck (yet)
        }
        catch (Exception e)
        {
            ModManager.Log($"[StuckVendorWatch] reflection read failed for vendor 0x{v.Guid.Full:X8}, falling back to OpenForBusiness only: {e.Message}");
            return null;
        }
    }

    private static List<Flag> ScanNow()
    {
        var flags = new List<Flag>();
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
        {
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
            {
                if (wo is not Vendor v || v.OpenForBusiness) continue;

                var overdue = SecondsPastDueReset(v);
                var reason = overdue.HasValue
                    ? $"closed, reset overdue by {(int)overdue.Value}s (likely stalled)"
                    : "closed right now (reset timer unreadable or not yet due - could be by design)";
                flags.Add(new Flag(lb.Id.Raw, v.Guid.Full, reason));
            }
        }
        return flags;
    }

    private static void Scan(Session? _)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var flags = ScanNow();
        if (flags.Count == 0) return;
        foreach (var p in PlayerManager.GetAllOnline())
        {
            if (p.Session == null || p.Session.AccessLevel < AccessLevel.Sentinel) continue;
            foreach (var f in flags)
                p.Session.Network.EnqueueSend(new GameMessageSystemChat(
                    $"StuckVendorWatch: vendor 0x{f.Guid:X8} in landblock {f.LandblockRaw >> 16:X4} - {f.Reason}",
                    ChatMessageType.Broadcast));
        }
    }

    [CommandHandler("vendorwatch", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
        "Lists vendors that are not open for business right now, flagging ones whose reset cycle looks stalled.", "")]
    public static void HandleVendorWatch(Session session, params string[] parameters)
    {
        var flags = ScanNow();
        void Send(string msg)
        {
            if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            else Console.WriteLine(msg);
        }

        Send("StuckVendorWatch is read-only: it never touches or resets a vendor. A vendor closed by design " +
             "(an event, a quest-gated shop) can show up here too - that's a false positive, not a bug; use " +
             "existing /reload-style stock commands to fix a genuinely stuck one by hand.");

        if (flags.Count == 0)
        {
            Send("No vendors are currently closed for business.");
            return;
        }
        foreach (var f in flags)
            Send($"Vendor 0x{f.Guid:X8} in landblock {f.LandblockRaw >> 16:X4} - {f.Reason}");
    }
}
