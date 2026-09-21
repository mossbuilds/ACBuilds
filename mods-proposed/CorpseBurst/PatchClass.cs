using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace CorpseBurst;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly Dictionary<uint, DateTime> LastUse = new();
    private static readonly object Gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // /burst is not one of ACE's built-in command names.
    [CommandHandler("burst", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Detonate the nearest monster corpse with a war spell.")]
    public static void HandleBurst(Session session, params string[] parameters)
    {
        var p = session?.Player;
        var cfg = Cfg;
        if (p == null || cfg == null || !cfg.Enabled) { if (session != null) Say(session, "CorpseBurst is switched off."); return; }
        lock (Gate)
        {
            if (LastUse.TryGetValue(p.Guid.Full, out var t) && (DateTime.UtcNow - t).TotalSeconds < cfg.CooldownSeconds)
            { Say(session!, "Not yet."); return; }
            LastUse[p.Guid.Full] = DateTime.UtcNow;
        }
        new ActionChain(p, () => DoBurst(p, cfg)).EnqueueChain();
    }

    private static void DoBurst(Player p, Settings cfg)
    {
        var session = p.Session;
        if (session == null) return;
        if (!Enum.TryParse<SpellId>(cfg.SpellId, out var id)) { Say(session, "CorpseBurst: bad SpellId in settings."); return; }
        var spell = new Spell(id);
        var lb = p.CurrentLandblock;
        if (lb == null || p.Location == null || spell._spell == null) return;

        Corpse? corpse = null; var bestD = float.MaxValue;
        foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
        {
            if (wo is not Corpse c || !c.IsMonster || c.IsDestroyed || c.Location == null) continue;
            if (c.VictimId != null && new ObjectGuid(c.VictimId.Value).IsPlayer()) continue;
            if (c.Location.Landblock != p.Location.Landblock || !c.HasPermission(p)) continue;
            if (cfg.CorpseMaxAgeSec > 0 && c.TimeToRot.HasValue && c.TimeToRot.Value < 0) continue;
            var d = p.Location.DistanceTo(c.Location);
            if (d <= cfg.Radius && d < bestD) { corpse = c; bestD = d; }
        }
        if (corpse == null) { Say(session, $"No monster corpse you can use within {cfg.Radius:0} units."); return; }

        // Aim at the living monster closest to the corpse; the spell's own targeting rules do the rest.
        Creature? aim = null; var aimD = float.MaxValue;
        foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
        {
            if (wo is not Creature m || wo is Player || m.IsDestroyed || !m.IsAlive || m.Location == null) continue;
            if (!p.CanDamage(m)) continue;
            var d = corpse.Location.DistanceTo(m.Location);
            if (d <= cfg.BurstRadius && d < aimD) { aim = m; aimD = d; }
        }
        if (aim == null) { Say(session, "Nothing near the corpse to burst on."); return; }

        if (cfg.ManaCost > 0)
        {
            if (p.Mana.Current < (uint)cfg.ManaCost) { Say(session, "You do not have enough mana."); return; }
            p.UpdateVitalDelta(p.Mana, -cfg.ManaCost);
        }
        corpse.Destroy();
        // Projectiles leave the PLAYER (a ring spell spreads 360 degrees around the caster), not the corpse.
        p.TryCastSpell(spell, aim, tryResist: false);
        Say(session, "The corpse bursts.");
    }
}
