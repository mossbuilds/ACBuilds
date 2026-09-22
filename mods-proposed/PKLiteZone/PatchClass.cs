using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PKLiteZone;

/// Approach: PlayerKillerStatus/PkLevel/PkLevelModifier are all public get/set on WorldObject
/// (WorldObject_Properties.cs), and PKModifier.cs proves direct external writes to them are the
/// normal way ACE itself flips PK state - so this mod writes those properties directly rather than
/// routing through any use-item/activation path. That matters because PKModifier.CheckUseRequirements
/// explicitly refuses to let an already-PKLite player use a PK obelisk to change status again
/// ("Player Killer Lites may not change their PK status") - going through that path would make our
/// own revert unable to run. Only NPK <-> PKLite is ever touched; a player already PK, Free or
/// otherwise flagged is left alone in both directions.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly HashSet<uint> OptedIn = new();
    private static HashSet<uint> zone = new();

    private static void Rebuild()
    {
        var s = new HashSet<uint>();
        if (Cfg != null)
            foreach (var h in Cfg.ZoneLandblocks)
                if (uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v)) s.Add(v);
        zone = s;
    }

    private static void Say(Session? s, string m)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(m, ChatMessageType.Broadcast));
        else Console.WriteLine(m);
    }

    private static void SetStatus(Player p, PlayerKillerStatus status, PKLevel level)
    {
        p.PlayerKillerStatus = status;
        p.PkLevel = level;
        p.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(p, PropertyInt.PlayerKillerStatus, (int)p.PlayerKillerStatus));
    }

    /// <summary>Applies or reverts PKLite for every online opted-in player based on current landblock. Returns (checked, changed).</summary>
    private static (int, int) Sweep()
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return (0, 0);
        int seen = 0, changed = 0;
        foreach (var guid in OptedIn.ToArray())
        {
            var p = PlayerManager.GetOnlinePlayer(guid);
            if (p == null) continue; // logout handled separately
            seen++;
            var inZone = p.Location != null && zone.Contains(p.Location.Landblock);
            if (inZone && p.PlayerKillerStatus == PlayerKillerStatus.NPK)
            {
                SetStatus(p, PlayerKillerStatus.PKLite, PKLevel.PKLite);
                Say(p.Session, "You have entered a PKLite zone: you can fight and be fought here with no item loss or vitae.");
                changed++;
            }
            else if (!inZone && p.PlayerKillerStatus == PlayerKillerStatus.PKLite)
            {
                SetStatus(p, PlayerKillerStatus.NPK, PKLevel.NPK);
                Say(p.Session, "You have left the PKLite zone: your PK status is back to normal.");
                changed++;
            }
        }
        return (seen, changed);
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        Rebuild();
        OptedIn.Clear();
        timer?.Dispose();
        var secs = Math.Max(2, Cfg.SweepSeconds);
        timer = new Timer(_ =>
        {
            try { Sweep(); }
            catch (Exception e) { ModManager.Log($"[PKLiteZone] {e.Message}"); }
        }, null, secs * 1000, secs * 1000);
        ModManager.Log("[PKLiteZone] ready" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    // Revert before the character saves on logout, same hook MinionCleanup uses for its own pre-logout cleanup.
    // Player.LogOut_Inner is public; verified against fresh source.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.LogOut_Inner), new[] { typeof(bool) })]
    public static void PreLogout(Player __instance)
    {
        if (Cfg is not { Enabled: true, RevertOnLogout: true }) return;
        if (!OptedIn.Contains(__instance.Guid.Full)) return;
        if (__instance.PlayerKillerStatus == PlayerKillerStatus.PKLite)
            SetStatus(__instance, PlayerKillerStatus.NPK, PKLevel.NPK);
        OptedIn.Remove(__instance.Guid.Full);
    }

    [CommandHandler("pklite", AccessLevel.Player, CommandHandlerFlag.None, 0,
        "Opt in or out of PKLite zones (retail's no item-loss, no-vitae sparring mode).", "[on|off]")]
    public static void HandlePkLite(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;
        var cfg = Cfg;
        if (cfg is not { Enabled: true })
        {
            Say(session, "PKLiteZone is disabled (Settings.json Enabled=false).");
            return;
        }

        var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";
        var guid = player.Guid.Full;

        switch (sub)
        {
            case "on":
                if (player.PlayerKillerStatus is PlayerKillerStatus.PK or PlayerKillerStatus.Free)
                {
                    Say(session, "You cannot opt into PKLite zones while your PK status is already PK or Free.");
                    return;
                }
                OptedIn.Add(guid);
                Say(session, "You have opted into PKLite zones. Your PK status will switch to PKLite while you are inside a designated zone, and back to normal when you leave, log out, or run /pklite off." + (zone.Count == 0 ? " (No zone is currently configured, so nothing will happen yet.)" : ""));
                break;
            case "off":
                OptedIn.Remove(guid);
                if (player.PlayerKillerStatus == PlayerKillerStatus.PKLite)
                    SetStatus(player, PlayerKillerStatus.NPK, PKLevel.NPK);
                Say(session, "You have opted out of PKLite zones. Your PK status is back to normal.");
                break;
            default:
                Say(session, $"PKLite: opted in = {OptedIn.Contains(guid)}, current status = {player.PlayerKillerStatus}. Use /pklite on or /pklite off.");
                break;
        }
    }

    [CommandHandler("pklitezone", AccessLevel.Admin, CommandHandlerFlag.None, 0,
        "View or set the PKLite zone landblocks (hex, space separated). Empty list clears the zone.", "[landblock ...]")]
    public static void HandlePkLiteZone(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null) return;

        if (parameters.Length == 0)
        {
            Say(session, $"PKLiteZone landblocks: {(zone.Count == 0 ? "(none configured)" : string.Join(", ", cfg.ZoneLandblocks))}. Opted-in players online: {OptedIn.Count(g => PlayerManager.GetOnlinePlayer(g) != null)}.");
            return;
        }

        var valid = new List<string>();
        foreach (var raw in parameters)
        {
            if (uint.TryParse(raw, System.Globalization.NumberStyles.HexNumber, null, out _))
                valid.Add(raw);
            else
                Say(session, $"Skipped '{raw}': not a valid hex landblock.");
        }

        cfg.ZoneLandblocks = valid;
        Rebuild();
        Say(session, $"PKLiteZone landblocks set to: {(valid.Count == 0 ? "(none)" : string.Join(", ", valid))}. This is runtime-only; edit Settings.json to persist it across a restart.");
    }
}
