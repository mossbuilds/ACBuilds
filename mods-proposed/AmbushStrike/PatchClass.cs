using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AmbushStrike;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(uint, uint), DateTime> last = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log($"[AmbushStrike] ready, enabled={Cfg?.Enabled}");
        return base.OnWorldOpen();
    }

    // DamageEvent.CalculateDamage(Creature, Creature, WorldObject, MotionCommand?, AttackHook) - static, returns DamageEvent
    // (DamageEvent.cs, ACE.Server.Entity). Called from Player.DamageTarget for melee/missile; Damage is a public float.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DamageEvent), nameof(DamageEvent.CalculateDamage), new Type[] { typeof(Creature), typeof(Creature), typeof(WorldObject), typeof(MotionCommand?), typeof(AttackHook) })]
    public static void PostCalc(Creature attacker, Creature defender, DamageEvent __result)
    {
        var c = Cfg;
        if (c == null || !c.Enabled || __result == null) return;
        if (attacker is not Player p || defender == null || defender is Player) return;
        if (!__result.HasDamage || __result.Damage <= 0) return;
        if (__result.CombatType != CombatType.Melee && __result.CombatType != CombatType.Missile) return;
        if (c.OnlyUnaware && defender.AttackTarget != null) return;
        if (c.RequireBehind && Math.Abs(defender.GetAngle(p)) <= 90.0f) return;
        if (!string.IsNullOrEmpty(c.RequirePathQuest) && !p.QuestManager.HasQuest(c.RequirePathQuest)) return;
        if (c.CooldownSecondsPerTarget > 0)
        {
            var key = (p.Guid.Full, defender.Guid.Full);
            var now = DateTime.UtcNow;
            if (last.TryGetValue(key, out var t) && (now - t).TotalSeconds < c.CooldownSecondsPerTarget) return;
            last[key] = now;
        }
        var m = Math.Clamp(c.Multiplier, 1.0, 5.0);
        if (m <= 1.0) return;
        __result.Damage = (float)(__result.Damage * m);
        p.Session?.Network.EnqueueSend(new GameMessageSystemChat($"Ambush! x{m:0.0#}", ChatMessageType.Broadcast));
    }

    [CommandHandler("ambush", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show or set the AmbushStrike multiplier (runtime only).", "[multiplier 1-5]")]
    public static void HandleAmbush(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        if (parameters.Length > 0 && double.TryParse(parameters[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            c.Multiplier = Math.Clamp(v, 1.0, 5.0);
        var msg = $"AmbushStrike: enabled={c.Enabled} x{c.Multiplier:0.0#} behind={c.RequireBehind} unaware={c.OnlyUnaware} quest='{c.RequirePathQuest}'";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
