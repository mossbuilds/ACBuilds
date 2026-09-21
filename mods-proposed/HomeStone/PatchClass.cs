using System.Text.Json;
using ACE.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace HomeStone;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private record Spot(uint Cell, float X, float Y, float Z, float RX, float RY, float RZ, float RW);

    private static readonly object gate = new();
    private static Dictionary<uint, Spot>? spots;
    private static readonly Dictionary<uint, DateTime> lastUse = new();

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    private static Dictionary<uint, Spot> Load(string file)
    {
        if (spots != null) return spots;
        try { spots = JsonSerializer.Deserialize<Dictionary<uint, Spot>>(File.ReadAllText(file)); } catch { }
        return spots ??= new();
    }

    // /home is a built-in ACE command, so these are /mark and /gomark.
    [CommandHandler("mark", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Save your current spot for /gomark.", "")]
    public static void HandleMark(Session session, params string[] parameters)
    {
        var cfg = Instance().SettingsContainer.Settings;
        var p = session.Player;
        var loc = p.Location;
        if (!cfg.AllowIndoors && loc.Indoors) { Say(session, "You cannot mark a spot indoors."); return; }
        lock (gate)
        {
            var d = Load(cfg.DataFile);
            d[p.Guid.Full] = new Spot(loc.Cell, loc.PositionX, loc.PositionY, loc.PositionZ,
                loc.Rotation.X, loc.Rotation.Y, loc.Rotation.Z, loc.Rotation.W);
            File.WriteAllText(cfg.DataFile, JsonSerializer.Serialize(d));
        }
        Say(session, "Spot saved. Use /gomark to return.");
    }

    [CommandHandler("gomark", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Teleport to your saved spot.", "")]
    public static void HandleGo(Session session, params string[] parameters)
    {
        var cfg = Instance().SettingsContainer.Settings;
        var p = session.Player;
        if (p.PKTimerActive) { Say(session, "You cannot do that so soon after combat."); return; }
        Spot? s;
        lock (gate)
        {
            Load(cfg.DataFile).TryGetValue(p.Guid.Full, out s);
            if (s != null && lastUse.TryGetValue(p.Guid.Full, out var t))
            {
                var left = cfg.CooldownSeconds - (DateTime.UtcNow - t).TotalSeconds;
                if (left > 0) { Say(session, $"Wait {(int)left + 1}s before using /gomark again."); return; }
            }
            if (s != null) lastUse[p.Guid.Full] = DateTime.UtcNow;
        }
        if (s == null) { Say(session, "No spot saved. Use /mark first."); return; }
        var pos = new Position(s.Cell, s.X, s.Y, s.Z, s.RX, s.RY, s.RZ, s.RW);
        p.Teleport(pos);
    }

    private static PatchClass? inst;
    private static PatchClass Instance() => inst!;
    public override Task OnStartSuccess() { inst = this; return base.OnStartSuccess(); }
}
