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

    private static bool OnPath(Player p, Settings cfg)
    {
        if (string.IsNullOrEmpty(cfg.RequirePath)) return true;
        return p.QuestManager.HasQuest("path_" + cfg.RequirePath);
    }

    /// <summary>
    /// Binds BurstSpellId to real spellcasting via WorldObject.HandleCastSpell (runs only after a successful
    /// cast - see RaiseSkeleton's PreHandleCastSpell for the verified hook details). Only a player's own
    /// direct cast is claimed; every other cast falls through untouched. DoBurst runs its own corpse/target
    /// search and mana check exactly as /burst does; it is called directly (no ActionChain) because
    /// HandleCastSpell already runs on the caster's action queue.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldObject), "HandleCastSpell", new[] { typeof(Spell), typeof(WorldObject), typeof(WorldObject), typeof(WorldObject), typeof(bool), typeof(bool), typeof(bool) })]
    public static bool PreHandleCastSpell(WorldObject __instance, Spell spell, WorldObject target, WorldObject itemCaster, bool fromProc, bool equip, ref bool __result)
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true } || __instance is not Player p || itemCaster != null || fromProc || equip)
            return true;

        var id = spell.Id;
        if (id == 0 || id != cfg.BurstSpellId) return true; // 0 = unbound; never claim the "no spell" sentinel

        if (!OnPath(p, cfg))
        {
            Say(p.Session, "Only a necromancer can shape this magic.");
            __result = false;
            return false;
        }

        DoBurst(p, cfg);
        __result = true;
        return false;
    }

    // Mana-only casting for the bound spells (Tom 2026-09-23). Player.HasComponentsForSpell(Spell) decides the "missing
    // components" error before the cast and Player.TryBurnComponents(Spell) uses them up after it - both public on
    // Player (WorldObjects/Player_Magic.cs lines 1227 and 1181). Only this mod's own bound ids, only for a necromancer.
    private static bool FreeFor(Player p, Spell spell)
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true, FreeComponents: true } || spell == null) return false;
        var id = spell.Id;
        if (id == 0 || !(id == cfg.BurstSpellId)) return false;
        return OnPath(p, cfg);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HasComponentsForSpell), new[] { typeof(Spell) })]
    public static bool PreHasComponentsForSpell(Player __instance, Spell spell, ref bool __result)
    {
        if (!FreeFor(__instance, spell)) return true;
        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.TryBurnComponents), new[] { typeof(Spell) })]
    public static bool PreTryBurnComponents(Player __instance, Spell spell) => !FreeFor(__instance, spell);
}
