using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace GentleDeath;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log($"[GentleDeath] ready, max items {Cfg.MaxItemsDropped}, vitae {Cfg.VitaeAmount}");
        return base.OnWorldOpen();
    }

    // Player.GetNumItemsDropped(Corpse) - verified in Player_Death.cs.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.GetNumItemsDropped), new Type[] { typeof(Corpse) })]
    public static void PostItems(Player __instance, Corpse corpse, ref int __result)
    {
        var c = Cfg;
        if (c == null || c.MaxItemsDropped < 0) return;
        if (c.SkipPkDeaths && corpse != null && corpse.PkLevel == PKLevel.PK) return;
        if (__result > c.MaxItemsDropped) __result = c.MaxItemsDropped;
    }

    // Player.InflictVitaePenalty(int amount = 5) - verified in Player_Death.cs.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.InflictVitaePenalty), new Type[] { typeof(int) })]
    public static void PreVitae(Player __instance, ref int amount)
    {
        var c = Cfg;
        if (c == null || c.VitaeAmount < 0) return;
        amount = Math.Max(1, Math.Min(amount, c.VitaeAmount));
    }

    [CommandHandler("gentledeath", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Show or set GentleDeath limits (runtime only, not saved).", "[maxItems vitaePercent]")]
    public static void HandleGentleDeath(Session session, params string[] parameters)
    {
        var c = Cfg;
        if (c == null) return;
        if (parameters.Length > 1 && int.TryParse(parameters[0], out var i) && int.TryParse(parameters[1], out var v) && i >= -1 && v >= -1)
        { c.MaxItemsDropped = i; c.VitaeAmount = v; }
        var msg = $"GentleDeath: max items {c.MaxItemsDropped}, vitae {c.VitaeAmount}%, skip PK {c.SkipPkDeaths}";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
