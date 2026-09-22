using ACE.Common;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace CorpseWatch;

// Verified against ACEmulator/ACE master (Source/ACE.Server/WorldObjects):
// - Corpse.EnterWorld(): "public override bool EnterWorld()" in Corpse.cs - runs once the corpse
//   object Creature_Death.cs's CreateCorpse() just built has been fully set up (VictimId/KillerId/
//   PkLevel/Location already assigned) and placed in the world. CreateCorpse() itself is
//   "protected void CreateCorpse(DamageHistoryInfo killer, bool hadVitae = false)" on Creature,
//   and the Corpse it builds is a *local variable* never returned or stored on a field - a plain
//   Harmony postfix on CreateCorpse cannot recover it. EnterWorld() is the first public, patchable
//   point where the finished Corpse instance itself is available as __instance, so this mod hooks
//   there instead of CreateCorpse or the (also protected) Player.Die - same "patch the protected
//   method or find the nearest public hook" call this repo's own DeathReport/MinionCleanup made.
// - Corpse.IsLooted: "public bool IsLooted { get; set; }" declared directly in Corpse.cs.
// - WorldObject.TimeToRot: "public double? TimeToRot { get; set; }" in WorldObject_Properties.cs
//   (backed by PropertyFloat.TimeToRot), inherited by Corpse (Corpse : Container : WorldObject).
// - WorldObject.CreationTimestamp: "public double? CreationTimestamp" (WorldObject_Properties.cs),
//   already used by Corpse.cs itself (RecalculateDecayTime/OnInitialInventoryLoadCompleted logging)
//   as the anchor for "when does TimeToRot run out": CreationTimestamp + TimeToRot, compared to
//   Time.GetUnixTime(), is the same math those ACE-native log lines use.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    private class Tracked
    {
        public required WeakReference<Corpse> Corpse;
        public required uint PlayerGuid;
        public HashSet<int> Warned = new();
    }

    // owning player's guid -> their most recent tracked corpse
    private static readonly Dictionary<uint, Tracked> tracked = new();
    // insertion order, so a mass-death event evicts the oldest entries first (MaxTracked)
    private static readonly LinkedList<uint> order = new();

    private static Timer? timer;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        timer?.Dispose();
        var secs = Math.Max(5, Cfg.SweepSeconds);
        timer = new Timer(_ => Sweep(), null, secs * 1000, secs * 1000);
        ModManager.Log("[CorpseWatch] ready: /mycorpse" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.EnterWorld))]
    public static void PostEnterWorld(Corpse __instance, bool __result)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || !__result) return;
            if (__instance.IsMonster || __instance.VictimId == null) return;

            var victim = new ObjectGuid(__instance.VictimId.Value);
            if (!victim.IsPlayer()) return;

            lock (gate)
            {
                var entry = new Tracked { Corpse = new WeakReference<Corpse>(__instance), PlayerGuid = victim.Full };
                tracked[victim.Full] = entry;
                order.Remove(victim.Full);
                order.AddLast(victim.Full);

                while (order.Count > Math.Max(1, cfg.MaxTracked))
                {
                    var oldest = order.First!.Value;
                    order.RemoveFirst();
                    tracked.Remove(oldest);
                }
            }
        }
        catch (Exception e) { ModManager.Log($"[CorpseWatch] {e.Message}"); }
    }

    /// <summary>Returns (found, corpseGone, secondsRemaining, isLooted). corpseGone==true means the tracked corpse object no longer exists.</summary>
    private static (bool found, bool corpseGone, double secondsRemaining, bool isLooted) Remaining(uint playerGuid)
    {
        lock (gate)
        {
            if (!tracked.TryGetValue(playerGuid, out var entry))
                return (false, false, 0, false);

            if (!entry.Corpse.TryGetTarget(out var corpse) || corpse.IsDestroyed)
            {
                tracked.Remove(playerGuid);
                order.Remove(playerGuid);
                return (true, true, 0, false);
            }

            if (corpse.IsLooted)
                return (true, false, 0, true);

            var expiresAt = (corpse.CreationTimestamp ?? Time.GetUnixTime()) + (corpse.TimeToRot ?? 0);
            var remaining = expiresAt - Time.GetUnixTime();
            return (true, false, Math.Max(0, remaining), false);
        }
    }

    private static void Sweep()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;

        List<uint> guids;
        lock (gate) { guids = new List<uint>(tracked.Keys); }

        foreach (var guid in guids)
        {
            Tracked? entry;
            lock (gate) { tracked.TryGetValue(guid, out entry); }
            if (entry == null) continue;

            var (found, corpseGone, remaining, isLooted) = Remaining(guid);
            if (!found) continue;

            if (corpseGone || isLooted)
                continue;

            var player = PlayerManager.GetOnlinePlayer(guid);
            if (player == null) continue;

            foreach (var warnAt in cfg.WarnSeconds)
            {
                if (remaining > warnAt) continue;
                bool alreadyWarned;
                lock (gate) { alreadyWarned = !entry.Warned.Add(warnAt); }
                if (alreadyWarned) continue;

                var mins = Math.Max(1, (int)Math.Ceiling(warnAt / 60.0));
                Whisper(player, $"Your corpse will decay or open to looting in about {mins} minute{(mins == 1 ? "" : "s")}.");
            }
        }
    }

    private static void Whisper(Player player, string msg) =>
        player.Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // /mycorpse is not a built-in ACE command name (grepped [CommandHandler] names).
    [CommandHandler("mycorpse", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows the time remaining before your most recent corpse decays or opens to looting.", "")]
    public static void HandleMyCorpse(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled || !cfg.AllowMyCorpse) { Whisper(session.Player, "/mycorpse is switched off."); return; }

        var p = session.Player;
        var (found, corpseGone, remaining, isLooted) = Remaining(p.Guid.Full);

        if (!found || corpseGone) { Whisper(p, "No corpse tracked since the server started (or it's gone)."); return; }
        if (isLooted) { Whisper(p, "Your corpse has already been opened."); return; }

        var mins = (int)(remaining / 60);
        var secs = (int)(remaining % 60);
        Whisper(p, $"Your corpse decays or opens to looting in about {mins}m {secs}s.");
    }
}
