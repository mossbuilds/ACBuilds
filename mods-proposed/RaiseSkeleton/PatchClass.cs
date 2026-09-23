using ACE.Server.Entity;
using ACE.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace RaiseSkeleton;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    // In memory only: owner guid -> live controlled minions. Lost on restart (the pets themselves are ephemeral world objects too).
    private static readonly object Gate = new();
    private static readonly Dictionary<uint, List<CombatPet>> Minions = new();
    private static readonly Dictionary<uint, DateTime> LastUse = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log("[RaiseSkeleton] ready: /raiseskel /minions /dismiss" + (Cfg is { Enabled: true } ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    private static bool Dead(CombatPet m) => m.IsDestroyed || m.IsDead;

    /// <summary>Limit = floor(Self / Divisor) + BonusLimit, clamped to 0..MaxMinionsHardCap.</summary>
    private static int LimitFor(Player p, Settings cfg)
    {
        var div = Math.Max(1, cfg.Divisor);
        var limit = (int)(p.Self.Current / (uint)div) + cfg.BonusLimit;
        return Math.Clamp(limit, 0, Math.Max(0, cfg.MaxMinionsHardCap));
    }

    /// <summary>Live minions of this owner, dead or destroyed ones pruned. Caller must hold Gate.</summary>
    private static List<CombatPet> LiveOf(uint owner)
    {
        if (!Minions.TryGetValue(owner, out var list)) Minions[owner] = list = new List<CombatPet>();
        list.RemoveAll(Dead);
        return list;
    }

    private static Corpse? NearestCorpse(Player p, Settings cfg)
    {
        var lb = p.CurrentLandblock;
        if (lb == null || p.Location == null) return null;
        Corpse? best = null;
        var bestD = float.MaxValue;
        foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
        {
            // IsMonster is set by Creature_Death for monster corpses; a player corpse (IsMonster false) is never touched.
            if (wo is not Corpse c || !c.IsMonster || c.IsDestroyed || c.Location == null) continue;
            if (c.VictimId != null && new ObjectGuid(c.VictimId.Value).IsPlayer()) continue;
            if (c.Location.Landblock != p.Location.Landblock) continue;
            if (!c.HasPermission(p)) continue;
            if (cfg.MinCorpseLevel > 0 && (c.Level ?? 0) < cfg.MinCorpseLevel) continue;
            var d = p.Location.DistanceTo(c.Location);
            if (d <= cfg.Radius && d < bestD) { best = c; bestD = d; }
        }
        return best;
    }

    [CommandHandler("raiseskel", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Raise the nearest monster corpse as a skeleton minion (feral if you are over your control limit).")]
    public static void HandleRaise(Session session, params string[] parameters)
    {
        var p = session?.Player;
        var cfg = Cfg;
        if (p == null || cfg == null || !cfg.Enabled) { if (session != null) Say(session, "Raise Skeleton is not enabled."); return; }
        RaiseFromNearestCorpse(p, cfg);
    }

    /// <summary>Shared by /raiseskel and the spell-bound cast hook. Applies its own per-player cooldown.</summary>
    public static void RaiseFromNearestCorpse(Player p, Settings cfg)
    {
        var session = p.Session;
        lock (Gate)
        {
            if (LastUse.TryGetValue(p.Guid.Full, out var t) && (DateTime.UtcNow - t).TotalSeconds < cfg.CooldownSeconds)
            {
                if (session != null) Say(session, "Your dark power is still gathering. Try again in a moment.");
                return;
            }
            LastUse[p.Guid.Full] = DateTime.UtcNow;
        }

        // World work (destroying the corpse, EnterWorld) runs as an action on the player's landblock.
        new ActionChain(p, () => DoRaise(p, cfg)).EnqueueChain();
    }

    private static void DoRaise(Player p, Settings cfg)
    {
        var session = p.Session;
        if (session == null) return;

        var corpse = NearestCorpse(p, cfg);
        if (corpse == null) { Say(session, $"There is no monster corpse you can raise within {cfg.Radius:0} units."); return; }

        if (cfg.ManaCost > 0)
        {
            if (p.Mana.Current < (uint)cfg.ManaCost) { Say(session, "You do not have enough mana."); return; }
            p.UpdateVitalDelta(p.Mana, -cfg.ManaCost);
        }

        int limit = LimitFor(p, cfg);
        int have;
        lock (Gate) have = LiveOf(p.Guid.Full).Count;
        var controlled = have < limit;

        // The corpse (and anything still inside it) is consumed either way. Capture the spot first.
        corpse.Destroy();

        if (controlled)
        {
            var pet = SummonMinion(p, cfg);
            if (pet == null) { Say(session, "The bones will not rise."); return; }
            int now;
            lock (Gate) { var l = LiveOf(p.Guid.Full); l.Add(pet); now = l.Count; }
            Say(session, $"A skeleton rises to serve you. Minions: {now}/{limit}.");

            if (cfg.MinionMinutes > 0)
            {
                var chain = new ActionChain();
                chain.AddDelaySeconds(cfg.MinionMinutes * 60.0);
                chain.AddAction(pet, () => { if (!pet.IsDestroyed) pet.Destroy(); });
                chain.EnqueueChain();
            }
        }
        else
        {
            var feral = SummonFeral(p, cfg);
            Say(session, feral
                ? $"You are beyond your control ({have}/{limit}) - the skeleton rises FERAL and will not obey!"
                : "The bones will not rise.");
        }
    }

    /// <summary>
    /// The real ACE pet path (PetDevice.SummonCreature): create the CombatPet weenie, then Pet.Init(player, petDevice) -
    /// which sets the owner, names it, places it in front of the player and calls EnterWorld. ACE allows one active pet
    /// (Player.CurrentActivePet); we clear it around Init so several minions can coexist, and restore any real pet after.
    /// </summary>
    private static CombatPet? SummonMinion(Player p, Settings cfg)
    {
        var device = WorldObjectFactory.CreateNewWorldObject(cfg.DeviceWcid) as PetDevice;
        var pet = WorldObjectFactory.CreateNewWorldObject(cfg.MinionWcid) as CombatPet;
        if (device == null || pet == null) { pet?.Destroy(); return null; }

        var prior = p.CurrentActivePet;
        p.CurrentActivePet = null;
        bool? ok;
        try { ok = pet.Init(p, device); }
        finally { if (prior != null && !prior.IsDestroyed) p.CurrentActivePet = prior; }

        if (ok != true) { pet.Destroy(); return null; }
        return pet;
    }

    private static bool SummonFeral(Player p, Settings cfg)
    {
        var feral = WorldObjectFactory.CreateNewWorldObject(cfg.FeralWcid);
        if (feral == null) return false;
        feral.Location = p.Location.InFrontOf(2.5f);
        feral.Location.LandblockId = new LandblockId(feral.Location.GetCell());
        if (feral.EnterWorld()) return true;
        feral.Destroy();
        return false;
    }

    [CommandHandler("minions", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Lists your controlled skeleton minions and your control limit.")]
    public static void HandleMinions(Session session, params string[] parameters)
    {
        var p = session?.Player;
        var cfg = Cfg ?? new Settings();
        if (p == null) return;
        List<CombatPet> live;
        lock (Gate) live = LiveOf(p.Guid.Full).ToList();
        Say(session!, $"Minions: {live.Count}/{LimitFor(p, cfg)} (limit from Self {p.Self.Current}).");
        var i = 1;
        foreach (var m in live)
            Say(session!, $"  {i++}. {m.Name}: health {m.Health.Current}/{m.Health.MaxValue}");
    }

    [CommandHandler("dismiss", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Destroys all your controlled skeleton minions.")]
    public static void HandleDismiss(Session session, params string[] parameters)
    {
        var p = session?.Player;
        if (p == null) return;
        DismissAll(p);
    }

    /// <summary>Shared by /dismiss and the spell-bound cast hook.</summary>
    public static void DismissAll(Player p)
    {
        List<CombatPet> live;
        lock (Gate) { live = LiveOf(p.Guid.Full).ToList(); Minions[p.Guid.Full].Clear(); }
        foreach (var m in live)
        {
            var pet = m;
            new ActionChain(pet, () => { if (!pet.IsDestroyed) pet.Destroy(); }).EnqueueChain();
        }
        if (p.Session != null)
            Say(p.Session, live.Count == 0 ? "You have no minions." : $"You dismiss {live.Count} minion(s).");
    }

    /// <summary>True if RequirePath is empty, or the player holds that PathChoice quest stamp. PathChoice is
    /// an optional sibling mod, so this reads the quest stamp directly (PathChoice.README: "Alternative with
    /// no reference: player.QuestManager.HasQuest(\"path_necromancer\")") rather than referencing its assembly.</summary>
    private static bool OnPath(Player p, Settings cfg)
    {
        if (string.IsNullOrEmpty(cfg.RequirePath)) return true;
        return p.QuestManager.HasQuest("path_" + cfg.RequirePath);
    }

    /// <summary>
    /// Binds RaiseSpellId/DismissSpellId to real spellcasting: WorldObject.HandleCastSpell runs only after
    /// a cast has succeeded (mana/components spent, fizzle roll passed upstream in Player_Magic.cs; verified
    /// ACE master, Source/ACE.Server/WorldObjects/WorldObject_Magic.cs line 259). Only a player's own direct
    /// cast (no item caster, not a proc, not an equip enchantment) of one of our own ids is claimed; every
    /// other cast (monsters, items, procs, other mods' ids) falls through to stock behaviour untouched.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldObject), "HandleCastSpell", new[] { typeof(Spell), typeof(WorldObject), typeof(WorldObject), typeof(WorldObject), typeof(bool), typeof(bool), typeof(bool) })]
    public static bool PreHandleCastSpell(WorldObject __instance, Spell spell, WorldObject target, WorldObject itemCaster, bool fromProc, bool equip, ref bool __result)
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true } || __instance is not Player p || itemCaster != null || fromProc || equip)
            return true;

        var id = spell.Id;
        if (id != cfg.RaiseSpellId && id != cfg.DismissSpellId) return true;
        if (id == 0) return true; // 0 = unbound; never claim the "no spell" sentinel

        if (!OnPath(p, cfg))
        {
            Say(p.Session!, "Only a necromancer can shape this magic.");
            __result = false;
            return false;
        }

        if (id == cfg.RaiseSpellId) RaiseFromNearestCorpse(p, cfg);
        else DismissAll(p);

        __result = true;
        return false;
    }
}
