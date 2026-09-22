using System.Globalization;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ZoneSweep;

// A scoped, preview-then-confirm alternative to PlayerManager.BootAllPlayers() (ACE.Server.Managers), which is
// server-wide, unconditional and has no preview - not what a "clear this one zone" admin action should reach for.
// This never touches BootAllPlayers. It only ever affects players whose current Location.Landblock equals the
// named target, and it logs each affected player off with their own Player.LogOut() (ACE's normal, graceful
// logout path - the same one a client-initiated logout takes, verified in Player.cs/Player_Networking.cs),
// never a session kill, never a character/item edit.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    // One pending preview per landblock, so a stale confirm for a different (or re-populated) zone is rejected.
    private class Pending
    {
        public uint Landblock;
        public HashSet<uint> Guids = new();
        public double At;
    }
    private static Pending? pending;

    private static double Now() => ACE.Common.Time.GetUnixTime(); // verified ACE.Common.Time.GetUnixTime()

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session? session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Landblock = Position.Landblock (ACE.Entity.Position: "public uint Landblock => landblockId.Raw >> 16"),
    // the top 16 bits of the cell id, printed as 4-digit hex (see TradeLedger's "lb {0:X4}").
    // Accepts "0198", "198", or "0x198".
    private static bool TryParseLandblock(string s, out uint lb)
    {
        lb = 0;
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lb);
    }

    // Players.Location.Landblock (ACE.Entity.Position, via Player.Location, WorldObject.Location) - verified
    // pattern already used in TradeLedger's log line (__instance.Location?.Landblock ?? 0).
    private static List<Player> PlayersInLandblock(uint landblock) =>
        PlayerManager.GetAllOnline() // verified: PlayerManager.GetAllOnline() returns List<Player>
            .Where(p => p.Location != null && p.Location.Landblock == landblock)
            .ToList();

    private static void Write(Settings cfg, string line)
    {
        lock (gate)
        {
            try
            {
                var fi = new FileInfo(cfg.LogFile);
                if (fi.Exists && fi.Length > cfg.MaxKb * 1024L)
                {
                    for (var i = cfg.MaxFiles; i >= 1; i--)
                    {
                        var src = i == 1 ? cfg.LogFile : $"{cfg.LogFile}.{i - 1}";
                        var dst = $"{cfg.LogFile}.{i}";
                        if (!File.Exists(src)) continue;
                        if (File.Exists(dst)) File.Delete(dst);
                        File.Move(src, dst);
                    }
                }
                File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
            }
            catch (Exception e) { ModManager.Log($"[ZoneSweep] log write: {e.Message}"); }
        }
    }

    // Bulk, player-affecting and irreversible for the session (a forced logoff), so this sits at the same bar as
    // TimedMute's staff-only, other-player-affecting command (Sentinel) rather than the Player self-service bar
    // (SkillRespec) or the Admin-only global toggles used elsewhere in this mod set. It is not raised to Admin:
    // the preview + short confirm window + single-landblock scope already bound the blast radius the way ACE's
    // own Sentinel-tier moderation commands (mute, teleport-to) are trusted to.
    [CommandHandler("zonesweep", AccessLevel.Sentinel, CommandHandlerFlag.None, 1,
        "Previews, then (with 'confirm') logs off every online player in one landblock. Never server-wide.",
        "<landblock hex> [confirm]")]
    public static void HandleZoneSweep(Session? session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ZoneSweep is not enabled on this server."); return; }
        if (!TryParseLandblock(parameters[0], out var lb))
        { Say(session, "Usage: /zonesweep <landblock hex, e.g. 0198> [confirm]"); return; }

        bool confirmWord = parameters.Length > 1 && parameters[1].Equals("confirm", StringComparison.OrdinalIgnoreCase);
        var now = Now();
        var here = PlayersInLandblock(lb);

        if (here.Count == 0)
        {
            lock (gate) { if (pending != null && pending.Landblock == lb) pending = null; }
            Say(session, $"No online players in landblock {lb:X4}. Nothing to do.");
            return;
        }

        bool confirmed;
        lock (gate)
        {
            confirmed = confirmWord
                && pending != null
                && pending.Landblock == lb
                && (now - pending.At) <= cfg.ConfirmSeconds
                && pending.Guids.SetEquals(here.Select(p => p.Guid.Full));
            if (!confirmed)
            {
                // Any mismatch (no prior preview, expired window, or the roster changed since the preview)
                // starts a fresh preview instead of proceeding - never act on a stale confirm.
                pending = new Pending { Landblock = lb, Guids = here.Select(p => p.Guid.Full).ToHashSet(), At = now };
                Say(session, $"Landblock {lb:X4}: {here.Count} player(s) online - " +
                    string.Join(", ", here.Select(p => p.Name)) +
                    $". Type /zonesweep {parameters[0]} confirm within {cfg.ConfirmSeconds}s to log them off.");
                return;
            }
            pending = null;
        }

        var by = session?.Player?.Name ?? "console";
        ModManager.Log($"[ZoneSweep] {by} swept landblock {lb:X4}: {here.Count} player(s)");
        Write(cfg, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | by {by} | landblock {lb:X4} | {here.Count} player(s)");
        Say(session, $"Sweeping landblock {lb:X4}: warning {here.Count} player(s), logoff in {cfg.WarnSeconds}s.");

        foreach (var p in here)
        {
            var warnMsg = string.Format(cfg.WarnMessage, cfg.WarnSeconds);
            p.Session?.Network.EnqueueSend(new GameMessageSystemChat(warnMsg, ChatMessageType.Broadcast));
            // Delay + the actual logoff both run as queued actions on the player's own actor, never straight off
            // this command/timer thread (per repo convention: any world/session work off the player's actor is
            // queued with ActionChain).
            var chain = new ActionChain();
            chain.AddDelaySeconds(cfg.WarnSeconds);
            chain.AddAction(p, () =>
            {
                // Re-check: they may have already left the zone or logged off themselves during the delay.
                if (p.Location != null && p.Location.Landblock == lb)
                    p.LogOut(); // Player.LogOut(bool clientSessionTerminatedAbruptly = false, bool forceImmediate = false)
                                 // - ACE's own graceful, per-player logout path (Player.cs), never a session kill.
            });
            chain.EnqueueChain();
        }
    }
}
