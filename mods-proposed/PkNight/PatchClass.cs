using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PkNight;

/// Approach: damage rules, not status changes. ACE's Player.SetPlayerKillerStatus only ever sets NPK or Free (PK/PKLite
/// are coerced to NPK) and PlayerManager.UpdatePKStatusForAllPlayers rewrites offline characters, so neither can
/// safely toggle PK for an event. Instead a Harmony postfix on Player.CheckPKStatusVsTarget clears the "not PK" refusal
/// for player-vs-player while the window is active. Nothing is stored on any character.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static volatile bool active;
    private static volatile int manual; // 0 = follow schedule, 1 = forced on, -1 = forced off
    private static bool warned;
    private static HashSet<uint> safe = new();

    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    private static void Rebuild()
    {
        var s = new HashSet<uint>();
        if (Cfg != null)
            foreach (var h in Cfg.SafeLandblocks)
                if (uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v)) s.Add(v);
        safe = s;
    }

    private static bool InWindow(Settings c, DateTime t, out TimeSpan untilStart)
    {
        untilStart = TimeSpan.MaxValue;
        if (!TimeSpan.TryParse(c.Start, out var st) || !TimeSpan.TryParse(c.End, out var en)) return false;
        bool DayOk(DateTime d) => c.Days.Any(x => string.Equals(x, d.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase));
        var now = t.TimeOfDay;
        if (st <= en)
        {
            if (DayOk(t) && now >= st && now < en) return true;
            if (DayOk(t) && now < st) untilStart = st - now;
            return false;
        }
        // crosses midnight: the window belongs to the day it starts on
        if (DayOk(t) && now >= st) return true;
        if (DayOk(t.AddDays(-1)) && now < en) return true;
        if (DayOk(t) && now < st) untilStart = st - now;
        return false;
    }

    private static void Set(bool on, string why)
    {
        if (active == on) return;
        active = on;
        Broadcast(on
            ? $"PK NIGHT HAS BEGUN ({why}): player-versus-player combat is now enabled. Stay in a safe area if you do not want to fight."
            : $"PK NIGHT HAS ENDED ({why}): player-versus-player combat is disabled again. Normal rules apply.");
        ModManager.Log($"[PkNight] active={on} ({why})");
    }

    private static void Tick()
    {
        var c = Cfg;
        if (c == null) return;
        if (!c.Enabled) { if (active) Set(false, "mod disabled"); return; }
        var inWin = InWindow(c, DateTime.Now, out var until);
        if (manual == 1) { Set(true, "started by an admin"); return; }
        if (manual == -1) { Set(false, "stopped by an admin"); return; }
        if (c.WarnMinutes > 0 && !inWin && !active && until <= TimeSpan.FromMinutes(c.WarnMinutes) && !warned)
        {
            warned = true;
            Broadcast($"PK night starts in about {(int)Math.Ceiling(until.TotalMinutes)} minutes. Player-versus-player combat will be enabled.");
        }
        if (until > TimeSpan.FromMinutes(c.WarnMinutes + 1)) warned = false;
        Set(inWin, "scheduled");
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        Rebuild();
        active = false;
        manual = 0;
        timer = new Timer(_ =>
        {
            try { Tick(); }
            catch (Exception e) { ModManager.Log($"[PkNight] {e.Message}"); }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        ModManager.Log($"[PkNight] ready, enabled={Cfg?.Enabled}");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        if (active) { active = false; Broadcast("PK NIGHT HAS ENDED (mod stopped): normal rules apply."); }
        base.Stop();
    }

    // Player.CheckPKStatusVsTarget(WorldObject, Spell) - verified in Player_Combat.cs (public override, null = allowed).
    // Only the NPK / not-same-PK-type refusals are cleared; house-boundary refusals stay.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.CheckPKStatusVsTarget), new Type[] { typeof(WorldObject), typeof(Spell) })]
    public static void PostCheckPK(Player __instance, WorldObject target, ref List<WeenieErrorWithString> __result)
    {
        if (!active || __result == null || __result.Count == 0) return;
        if (target is not Player tp || tp == __instance) return;
        if (safe.Contains(__instance.Location?.Landblock ?? 0) || safe.Contains(tp.Location?.Landblock ?? 0)) return;
        var e = __result[0];
        if (e == WeenieErrorWithString.YouFailToAffect_YouAreNotPK || e == WeenieErrorWithString.YouFailToAffect_TheyAreNotPK
            || e == WeenieErrorWithString.YouFailToAffect_NotSamePKType)
            __result = null;
    }

    private static void Say(Session s, string m)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(m, ChatMessageType.Broadcast));
        else Console.WriteLine(m);
    }

    [CommandHandler("pknight", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Start, stop or check PK night.", "[start|stop|auto|status]")]
    public static void HandlePkNight(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "status";
        if (sub != "status" && !c.Enabled) { Say(session, "PkNight is disabled (Settings.json Enabled=false)."); return; }
        switch (sub)
        {
            case "start": manual = 1; Tick(); break;
            case "stop": manual = -1; Tick(); break;
            case "auto": manual = 0; Tick(); break;
        }
        Say(session, $"PkNight: enabled={c.Enabled}, active={active}, control={(manual == 1 ? "manual on" : manual == -1 ? "manual off" : "schedule")}, window {string.Join(",", c.Days)} {c.Start}-{c.End}.");
    }
}
