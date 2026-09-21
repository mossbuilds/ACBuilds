using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace TreasureHunt;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const uint PyrealWcid = 273; // ACE coinStackWcid, as in SkillRespec
    private static Settings? Cfg;
    private static Timer? timer;
    private static readonly object gate = new();
    private static HuntDef? active;
    private static ACE.Entity.Position? target;
    private static DateTime endsAt;
    private static bool claiming;

    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    private static void Broadcast(string msg) =>
        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        if (Cfg.Enabled)
        {
            var p = TimeSpan.FromSeconds(Math.Max(1, Cfg.CheckSeconds));
            timer = new Timer(_ => Tick(), null, p, p);
        }
        ModManager.Log($"[TreasureHunt] ready, enabled={Cfg.Enabled}, {Cfg.Hunts.Count} hunts");
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        active = null;
        base.Stop();
    }

    private static ACE.Entity.Position? Build(HuntDef h)
    {
        if (!uint.TryParse(h.Cell, System.Globalization.NumberStyles.HexNumber, null, out var cell)) return null;
        return new ACE.Entity.Position(cell, h.X, h.Y, h.Z, 0, 0, 0, 1);
    }

    private static float? Dist(Player p)
    {
        var loc = p.Location; var t = target;
        if (loc == null || t == null || loc.Landblock != t.Landblock) return null;
        return loc.DistanceTo(t);
    }

    private static void Tick()
    {
        try
        {
            var cfg = Cfg; var h = active;
            if (cfg == null || h == null || claiming) return;
            if (DateTime.UtcNow >= endsAt)
            {
                lock (gate) { if (active != h) return; active = null; }
                Broadcast("The treasure hunt has ended with no winner.");
                return;
            }
            foreach (var p in PlayerManager.GetAllOnline())
            {
                var loc = p.Location; var t = target;
                if (loc == null || t == null || loc.Cell != t.Cell) continue;
                var d = Dist(p);
                if (d == null || d > h.Radius) continue;
                lock (gate) { if (active != h || claiming) return; claiming = true; }
                var winner = p;
                // Player work from a timer thread goes through the player's action queue.
                new ActionChain(winner, () =>
                {
                    try
                    {
                        lock (gate) { if (active != h) return; active = null; }
                        var amount = Math.Clamp(h.RewardPyreals, 0, Math.Max(0, cfg.MaxRewardPyreals));
                        Broadcast($"{winner.Name} solved the riddle and found the treasure!" + (amount > 0 ? $" Reward: {amount} pyreals." : ""));
                        if (amount > 0)
                        {
                            var coin = WorldObjectFactory.CreateNewWorldObject(PyrealWcid);
                            if (coin != null)
                            {
                                coin.SetStackSize(amount);
                                if (!winner.TryCreateInInventoryWithNetworking(coin))
                                    winner.Session.Network.EnqueueSend(new GameMessageSystemChat("Your pack is full; the reward could not be given. Tell an admin.", ChatMessageType.Broadcast));
                            }
                        }
                        ModManager.Log($"[TreasureHunt] won by {winner.Name}, reward {amount}");
                    }
                    catch (Exception e) { ModManager.Log($"[TreasureHunt] {e.Message}"); }
                    finally { lock (gate) { claiming = false; } }
                }).EnqueueChain();
                return;
            }
        }
        catch (Exception e) { ModManager.Log($"[TreasureHunt] {e.Message}"); }
    }

    [CommandHandler("hunt", AccessLevel.Admin, CommandHandlerFlag.None, 1, "Run a treasure hunt.", "start [index] | stop | status")]
    public static void HandleHunt(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "TreasureHunt is disabled in Settings.json."); return; }
        switch (parameters[0].ToLowerInvariant())
        {
            case "start":
                if (cfg.Hunts.Count == 0) { Say(session, "No hunts configured."); return; }
                var idx = 0;
                if (parameters.Length > 1 && !int.TryParse(parameters[1], out idx)) { Say(session, "Index must be a number."); return; }
                if (idx < 0 || idx >= cfg.Hunts.Count) { Say(session, $"Index 0-{cfg.Hunts.Count - 1}."); return; }
                var h = cfg.Hunts[idx];
                var pos = Build(h);
                if (pos == null) { Say(session, "That hunt has an invalid Cell."); return; }
                lock (gate)
                {
                    if (active != null) { Say(session, "A hunt is already running (/hunt stop first)."); return; }
                    target = pos; endsAt = DateTime.UtcNow.AddMinutes(Math.Max(1, cfg.MaxMinutes)); active = h;
                }
                Broadcast($"TREASURE HUNT! Riddle: {h.Riddle}  (type /hint for a distance hint)");
                break;
            case "stop":
                lock (gate) { active = null; }
                Broadcast("The treasure hunt was stopped.");
                break;
            default:
                var a = active;
                Say(session, a == null ? "No hunt running." : $"Hunt running until {endsAt:HH:mm} UTC. Riddle: {a.Riddle}");
                break;
        }
    }

    [CommandHandler("hint", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "How close are you to the treasure? (distance band only)", "")]
    public static void HandleHint(Session session, params string[] parameters)
    {
        var a = active;
        if (a == null || Cfg == null) { Say(session, "No treasure hunt is running."); return; }
        var d = Dist(session.Player);
        var r = a.Radius;
        string band = d == null ? "very far" : d <= r * 3 ? "hot" : d <= r * 10 ? "warm" : d <= r * 40 ? "far" : "very far";
        Say(session, $"You are {band}.");
    }
}
