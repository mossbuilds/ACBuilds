using ACE.Server.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace LoginShield;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static readonly Dictionary<uint, double> shield = new(); // character guid -> unix expiry; memory only

    private static double Now() => ACE.Common.Time.GetUnixTime();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    public override void Stop()
    {
        lock (gate) shield.Clear();
        base.Stop();
    }

    private static bool Active(uint guid)
    {
        lock (gate)
        {
            if (!shield.TryGetValue(guid, out var until)) return false;
            if (until > Now()) return true;
            shield.Remove(guid);
            return false;
        }
    }

    private static void End(uint guid) { lock (gate) shield.Remove(guid); }

    // Player.PlayerEnterWorld verified in Player_Networking.cs.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnter(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || cfg.ShieldSeconds <= 0) return;
            var p = __instance;
            if (p.Session != null && p.Session.AccessLevel > AccessLevel.Player) return; // staff never need it
            lock (gate) shield[p.Guid.Full] = Now() + cfg.ShieldSeconds;
            if (cfg.Notify)
                new ActionChain(p, () => { }).AddDelaySeconds(3).AddAction(p, () =>
                    p.Session?.Network.EnqueueSend(new GameMessageSystemChat(string.Format(cfg.StartMessage, cfg.ShieldSeconds), ChatMessageType.Broadcast))).EnqueueChain();
        }
        catch (Exception e) { ModManager.Log($"[LoginShield] {e.Message}"); }
    }

    // Player.TakeDamage(WorldObject, DamageType, float, BodyPart, bool, AttackConditions) - same signature PkGuard uses; verified in Player_Combat.cs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.TakeDamage), new Type[] { typeof(WorldObject), typeof(DamageType), typeof(float), typeof(BodyPart), typeof(bool), typeof(AttackConditions) })]
    public static bool PreTakeDamage(Player __instance, ref int __result)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled || !Active(__instance.Guid.Full)) return true;
        __result = 0;
        return false;
    }

    private static void Broke(Player p)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled || !cfg.BreakOnAttack) return;
        End(p.Guid.Full);
    }

    // Attack entry points, verified in Player_Melee/Missile/Magic.cs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionTargetedMeleeAttack), new Type[] { typeof(uint), typeof(uint), typeof(float) })]
    public static void PreMelee(Player __instance) => Broke(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionTargetedMissileAttack), new Type[] { typeof(uint), typeof(uint), typeof(float) })]
    public static void PreMissile(Player __instance) => Broke(__instance);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionCastTargetedSpell), new Type[] { typeof(uint), typeof(uint), typeof(WorldObject) })]
    public static void PreSpell(Player __instance) => Broke(__instance);

    [CommandHandler("loginshield", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Shows how many characters are currently shielded.", "")]
    public static void HandleShield(Session session, params string[] parameters)
    {
        int n; lock (gate) n = shield.Count(kv => kv.Value > Now());
        var msg = $"LoginShield: {n} shielded, enabled={Cfg?.Enabled}.";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
