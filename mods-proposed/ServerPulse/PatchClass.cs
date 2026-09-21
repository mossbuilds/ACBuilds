using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ServerPulse;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Reply(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Verified: LandblockManager.GetLoadedLandblocks() returns a ToList() snapshot under a read lock;
    // Landblock.GetAllWorldObjectsForDiagnostics() returns worldObjects.Values.ToList() (built for cross-thread reads);
    // Landblock.Id is ACE.Entity.LandblockId (Raw); PlayerManager.GetOnlineCount().
    [CommandHandler("pulse", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0, "Shows a read-only server health snapshot.", "[top]")]
    public static void HandlePulse(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        int top = Math.Max(1, cfg.TopN);
        if (parameters.Length > 0 && int.TryParse(parameters[0], out var n)) top = n;
        top = Math.Clamp(top, 1, Math.Max(1, cfg.MaxTopN));

        var blocks = LandblockManager.GetLoadedLandblocks();
        var rows = new List<(uint id, int players, int creatures, bool dormant)>();
        foreach (var lb in blocks)
        {
            int players = 0, creatures = 0;
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
            {
                if (wo is Player) players++;
                else if (wo is Creature) creatures++;
            }
            rows.Add((lb.Id.Raw, players, creatures, lb.IsDormant));
        }

        Reply(session, $"Pulse: {PlayerManager.GetOnlineCount()} online, {blocks.Count} landblocks loaded ({rows.Count(r => r.dormant)} dormant), {rows.Sum(r => r.creatures)} creatures.");
        foreach (var r in rows.OrderByDescending(r => r.players * 1000 + r.creatures).Take(top))
            Reply(session, $"  {r.id >> 16:X4}: {r.players} players, {r.creatures} creatures{(r.dormant ? " (dormant)" : "")}");
    }
}
