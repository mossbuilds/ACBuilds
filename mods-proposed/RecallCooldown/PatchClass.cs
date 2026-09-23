using ACE.Common;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace RecallCooldown;

/// Gate point: private void WorldObject.HandleCastSpell_PortalRecall(Spell spell, Creature targetCreature)
/// (Source/ACE.Server/WorldObjects/WorldObject_Magic.cs line 1046, ACE master), reached only from
/// HandleCastSpell's `case SpellType.PortalRecall:` (line 313-315). All five recall spells go through it:
/// PortalRecall, LifestoneRecall1, LifestoneSending1, PortalTieRecall1, PortalTieRecall2.
/// Private, so patched by name with an explicit argument-type array.
///
/// Safety: returning false skips the method before any ActionChain is built, so DoPreTeleportHide never
/// runs and nobody is left hidden/mid-teleport. Components and mana were already spent earlier in the cast
/// pipeline (Player_Magic.cs) - exactly as they are for the method's own early returns (Olthoi, PK timer,
/// "You must link to a portal"), so a blocked recall looks like any other failed recall to the player.
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    // target player guid -> unix time of last allowed recall. In memory only, never the DB.
    private static readonly Dictionary<uint, double> LastRecall = new();
    private static readonly object Gate = new();
    private static long blocked;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        lock (Gate) LastRecall.Clear();
        timer?.Dispose();
        var secs = Math.Max(30, Cfg.PruneSeconds);
        timer = new Timer(_ =>
        {
            try { Prune(); }
            catch (Exception e) { ModManager.Log($"[RecallCooldown] {e.Message}"); }
        }, null, secs * 1000, secs * 1000);
        ModManager.Log("[RecallCooldown] ready" + (Cfg.Enabled && Cfg.CooldownSeconds > 0 ? $" ({Cfg.CooldownSeconds}s shared cooldown)" : " (inactive: Enabled=false or CooldownSeconds=0)"));
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        timer?.Dispose();
        timer = null;
        base.Stop();
    }

    private static int Prune()
    {
        var cd = Cfg?.CooldownSeconds ?? 0;
        var now = Time.GetUnixTime();
        lock (Gate)
        {
            var stale = LastRecall.Where(kv => now - kv.Value >= cd).Select(kv => kv.Key).ToList();
            foreach (var k in stale) LastRecall.Remove(k);
            return stale.Count;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldObject), "HandleCastSpell_PortalRecall", new[] { typeof(Spell), typeof(Creature) })]
    public static bool PreRecall(Creature targetCreature)
    {
        try
        {
            var cfg = Cfg;
            if (cfg is not { Enabled: true } || cfg.CooldownSeconds <= 0) return true;
            if (targetCreature is not Player p) return true;
            if (cfg.ExemptInPk && p.PlayerKillerStatus == PlayerKillerStatus.PK) return true;

            var now = Time.GetUnixTime();
            var key = p.Guid.Full;
            lock (Gate)
            {
                if (LastRecall.TryGetValue(key, out var last))
                {
                    var left = cfg.CooldownSeconds - (now - last);
                    if (left > 0)
                    {
                        blocked++;
                        p.Session?.Network.EnqueueSend(new GameMessageSystemChat(
                            $"Your recall magic has not settled yet. You can recall again in {Math.Ceiling(left)} seconds.",
                            ChatMessageType.Magic));
                        return false;
                    }
                }
                LastRecall[key] = now;
            }
            return true;
        }
        catch (Exception e)
        {
            // Never let a mod fault block a recall.
            ModManager.Log($"[RecallCooldown] prefix error, allowing recall: {e.Message}");
            return true;
        }
    }

    [CommandHandler("recallcooldown", AccessLevel.Admin, CommandHandlerFlag.None, 0,
        "Show RecallCooldown status, or prune expired entries.", "[prune]")]
    public static void HandleStatus(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        string msg;
        if (cfg == null) msg = "RecallCooldown not initialised.";
        else if (parameters.Length > 0 && parameters[0].Equals("prune", StringComparison.OrdinalIgnoreCase))
            msg = $"RecallCooldown: pruned {Prune()} expired entries.";
        else
        {
            int n; lock (Gate) n = LastRecall.Count;
            msg = $"RecallCooldown: Enabled={cfg.Enabled}, CooldownSeconds={cfg.CooldownSeconds}, ExemptInPk={cfg.ExemptInPk}, tracked={n}, blocked since start={blocked}.";
        }
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
