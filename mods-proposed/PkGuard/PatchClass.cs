using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PkGuard;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static HashSet<uint> blocks = new();

    private static void Rebuild()
    {
        var set = new HashSet<uint>();
        if (Cfg != null)
            foreach (var s in Cfg.Landblocks)
                if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v)) set.Add(v);
        blocks = set;
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        Rebuild();
        ModManager.Log($"[PkGuard] ready, {blocks.Count} protected landblocks");
        return base.OnWorldOpen();
    }

    // Player.TakeDamage(WorldObject, DamageType, float, BodyPart, bool, AttackConditions) - verified in Player_Combat.cs.
    // Returning false skips the original; __result is the damage dealt (0). Covers melee and missile; PvP spells may still land.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.TakeDamage), new Type[] { typeof(WorldObject), typeof(DamageType), typeof(float), typeof(BodyPart), typeof(bool), typeof(AttackConditions) })]
    public static bool PreTakeDamage(Player __instance, WorldObject source, ref int __result)
    {
        if (blocks.Count == 0 || source is not Player) return true;
        if (source == __instance) return true;
        var lb = __instance.Location?.Landblock ?? 0;
        if (!blocks.Contains(lb)) return true;
        __result = 0;
        return false;
    }

    [CommandHandler("pkguard", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show, add or remove a protected landblock (runtime only).", "[add|remove HEX]")]
    public static void HandlePkGuard(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        if (parameters.Length > 1 && uint.TryParse(parameters[1], System.Globalization.NumberStyles.HexNumber, null, out _))
        {
            var hex = parameters[1].ToUpperInvariant();
            if (parameters[0] == "add" && !c.Landblocks.Contains(hex)) c.Landblocks.Add(hex);
            else if (parameters[0] == "remove") c.Landblocks.Remove(hex);
            Rebuild();
        }
        var msg = $"PkGuard landblocks: {(c.Landblocks.Count == 0 ? "(none)" : string.Join(",", c.Landblocks))}";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
