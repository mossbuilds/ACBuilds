using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace DeathReport;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static readonly Dictionary<uint, Position> lastDeath = new();
    private static readonly Dictionary<uint, DateTime> lastUse = new();
    private static DateTime lastBroadcast = DateTime.MinValue;
    private static readonly Random rng = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    // Player.OnDeath(DamageHistoryInfo, DamageType, bool) is public override in Player_Death.cs (returns DeathMessage); runs before the teleport to the lifestone.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.OnDeath), new Type[] { typeof(DamageHistoryInfo), typeof(DamageType), typeof(bool) })]
    public static void PostOnDeath(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            var pos = new Position(__instance.Location);
            var send = false;
            lock (gate)
            {
                lastDeath[__instance.Guid.Full] = pos;
                if (cfg.Broadcast && (DateTime.UtcNow - lastBroadcast).TotalSeconds >= cfg.BroadcastCooldownSeconds)
                {
                    lastBroadcast = DateTime.UtcNow; send = true;
                }
            }
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | {__instance.Name} | {pos.ToLOCString()}";
            lock (gate) File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
            if (send && cfg.Messages.Count > 0)
            {
                var msg = string.Format(cfg.Messages[rng.Next(cfg.Messages.Count)], __instance.Name);
                ACE.Server.Managers.PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            }
        }
        catch (Exception e) { ModManager.Log($"[DeathReport] {e.Message}"); }
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // /lastdeath is not a built-in ACE command name (grepped [CommandHandler] names).
    [CommandHandler("lastdeath", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Teleport back to where you last died.", "")]
    public static void HandleLastDeath(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled || !cfg.AllowLastDeath) { Say(session, "/lastdeath is switched off."); return; }
        var p = session.Player;
        Position? pos;
        lock (gate)
        {
            if (!lastDeath.TryGetValue(p.Guid.Full, out pos)) { Say(session, "No death recorded since the server started."); return; }
            if (lastUse.TryGetValue(p.Guid.Full, out var t) && (DateTime.UtcNow - t).TotalSeconds < cfg.LastDeathCooldownSeconds)
            { Say(session, "/lastdeath is cooling down."); return; }
            lastUse[p.Guid.Full] = DateTime.UtcNow;
        }
        Say(session, "Returning to where you fell...");
        p.Teleport(new Position(pos));
    }
}
