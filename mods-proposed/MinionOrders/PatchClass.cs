using System.Runtime.CompilerServices;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace MinionOrders;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    private enum Order { Follow, Hold, Attack }

    private sealed class OrderState
    {
        public Order Order = Order.Follow;
        public uint Target;
    }

    // Mod-side only, no DB. Absent entry = stock behaviour.
    private static readonly ConditionalWeakTable<CombatPet, OrderState> States = new();
    private static readonly Dictionary<uint, long> LastUse = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log("[MinionOrders] ready: /order attack|hold|follow" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    private static OrderState? Get(CombatPet p) => States.TryGetValue(p, out var s) ? s : null;

    // Hold: never look for targets. Attack: keep the ordered target while valid, else fall back to stock.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CombatPet), nameof(CombatPet.HandleFindTarget))]
    public static bool PreHandleFindTarget(CombatPet __instance)
    {
        if (Cfg is not { Enabled: true }) return true;
        var st = Get(__instance);
        if (st == null || st.Order == Order.Follow) return true;
        if (st.Order == Order.Hold) { __instance.AttackTarget = null; return false; }
        var t = __instance.CurrentLandblock?.GetObject(new ACE.Entity.ObjectGuid(st.Target)) as Creature;
        if (t == null || t.IsDead || !__instance.CanDamage(t))
        {
            st.Order = Order.Follow; // target gone: resume stock target choice
            return true;
        }
        __instance.AttackTarget = t;
        return false;
    }

    // Movement() calls FindNextTarget directly when the target is too far; honour Hold and Attack there too.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CombatPet), nameof(CombatPet.FindNextTarget))]
    public static bool PreFindNextTarget(CombatPet __instance, ref bool __result)
    {
        if (Cfg is not { Enabled: true }) return true;
        var st = Get(__instance);
        if (st == null || st.Order == Order.Follow) return true;
        if (st.Order == Order.Hold) { __instance.AttackTarget = null; __result = false; return false; }
        __result = __instance.AttackTarget != null;
        return false;
    }

    private static void Say(Session? s, string msg)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    [CommandHandler("order", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Gives an order to your skeleton minions.", "attack | hold | follow")]
    public static void HandleOrder(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var player = session?.Player;
        if (cfg is not { Enabled: true } || player == null) { Say(session, "Orders are not available."); return; }

        var now = Environment.TickCount64;
        lock (LastUse)
        {
            if (LastUse.TryGetValue(player.Guid.Full, out var last) && now - last < cfg.OrderCooldownMs) { Say(session, "Give the minions a moment."); return; }
            LastUse[player.Guid.Full] = now;
        }

        var verb = parameters[0].ToLowerInvariant();
        if (verb is not ("attack" or "hold" or "follow")) { Say(session, "Usage: /order attack | hold | follow"); return; }

        uint targetGuid = 0;
        if (verb == "attack")
        {
            // Player's selected creature (HealthQueryTarget is set when you select something).
            targetGuid = player.HealthQueryTarget ?? 0;
            var sel = targetGuid == 0 ? null : player.CurrentLandblock?.GetObject(new ACE.Entity.ObjectGuid(targetGuid)) as Creature;
            if (sel == null || sel.IsDead || sel is Player) { Say(session, "Select a monster first."); return; }
        }

        GiveOrder(player, cfg, verb, targetGuid);
    }

    /// <summary>Shared by /order and the spell-bound cast hook. targetGuid is only consulted for "attack".</summary>
    public static void GiveOrder(Player player, Settings cfg, string verb, uint targetGuid)
    {
        var owner = player.Guid.Full;
        int n = 0;
        foreach (var lb in LandblockManager.GetLoadedLandblocks())
            foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
            {
                if (wo is not CombatPet pet || pet.IsDestroyed || pet.PetOwner != owner || !cfg.MinionWcids.Contains(pet.WeenieClassId)) continue;
                n++;
                var p = pet;
                var tg = targetGuid;
                new ActionChain(p, () =>
                {
                    if (p.IsDestroyed) return;
                    var st = States.GetOrCreateValue(p);
                    switch (verb)
                    {
                        case "hold":
                            st.Order = Order.Hold; st.Target = 0;
                            p.AttackTarget = null;
                            p.CancelMoveTo(); // stop chasing; stands ready
                            break;
                        case "attack":
                            var t = p.CurrentLandblock?.GetObject(new ACE.Entity.ObjectGuid(tg)) as Creature;
                            if (t == null || t.IsDead || !p.CanDamage(t)) break;
                            st.Order = Order.Attack; st.Target = tg;
                            p.AttackTarget = t;
                            break;
                        default:
                            st.Order = Order.Follow; st.Target = 0; // stock AI resumes
                            break;
                    }
                }).EnqueueChain();
            }
        Say(player.Session, n == 0 ? "You have no minions to command." : "Order '" + verb + "' given to " + n + " minion(s).");
    }

    private static bool OnPath(Player p, Settings cfg)
    {
        if (string.IsNullOrEmpty(cfg.RequirePath)) return true;
        return p.QuestManager.HasQuest("path_" + cfg.RequirePath);
    }

    /// <summary>
    /// Binds AttackSpellId/HoldSpellId/FollowSpellId to real spellcasting via WorldObject.HandleCastSpell
    /// (runs only after a successful cast - see RaiseSkeleton's PreHandleCastSpell for the verified hook
    /// details). Only a player's own direct cast is claimed; every other cast falls through untouched.
    /// For "attack", the cast's own target is used when it is a hostile Creature; otherwise falls back to
    /// the player's current selection (HealthQueryTarget), same as /order attack.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorldObject), "HandleCastSpell", new[] { typeof(Spell), typeof(WorldObject), typeof(WorldObject), typeof(WorldObject), typeof(bool), typeof(bool), typeof(bool) })]
    public static bool PreHandleCastSpell(WorldObject __instance, Spell spell, WorldObject target, WorldObject itemCaster, bool fromProc, bool equip, ref bool __result)
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true } || __instance is not Player p || itemCaster != null || fromProc || equip)
            return true;

        var id = spell.Id;
        if (id == 0) return true; // 0 = unbound; never claim the "no spell" sentinel
        string? verb = id == cfg.AttackSpellId ? "attack" : id == cfg.HoldSpellId ? "hold" : id == cfg.FollowSpellId ? "follow" : null;
        if (verb == null) return true;

        if (!OnPath(p, cfg))
        {
            Say(p.Session, "Only a necromancer can shape this magic.");
            __result = false;
            return false;
        }

        uint targetGuid = 0;
        if (verb == "attack")
        {
            var hostile = target as Creature;
            targetGuid = hostile != null && !hostile.IsDead && hostile is not Player && p.CanDamage(hostile) ? hostile.Guid.Full : p.HealthQueryTarget ?? 0;
        }

        GiveOrder(p, cfg, verb, targetGuid);
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
        if (id == 0 || !(id == cfg.AttackSpellId || id == cfg.HoldSpellId || id == cfg.FollowSpellId)) return false;
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
