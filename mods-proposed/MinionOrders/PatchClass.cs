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
        Say(session, n == 0 ? "You have no minions to command." : "Order '" + verb + "' given to " + n + " minion(s).");
    }
}
