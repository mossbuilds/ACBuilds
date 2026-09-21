using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AutoLoot;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly HashSet<uint> enabled = new();
    private static readonly object gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // "loot" style names may clash; /autoloot is not a built-in ACE command (grepped the command handlers).
    [CommandHandler("autoloot", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Toggle auto-looting of coins/notes from monster corpses.", "")]
    public static void HandleAutoLoot(Session session, params string[] parameters)
    {
        var id = session.Player.Guid.Full;
        bool on;
        lock (gate) { on = enabled.Add(id); if (!on) enabled.Remove(id); }
        Say(session, on ? "AutoLoot on." : "AutoLoot off.");
    }

    private static bool Wanted(WorldObject item)
    {
        if (Cfg == null) return false;
        if (Cfg.Coins && item.WeenieType == WeenieType.Coin) return true;
        if (Cfg.TradeNotes && item.ItemType == ItemType.PromissoryNote) return true;
        if (Cfg.Gems && item.ItemType == ItemType.Gem) return true;
        return false;
    }

    // Corpse.Open(Player) verified in Corpse.cs (override of Container.Open). Runs after base.Open, so IsOpen means permission passed.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Corpse), nameof(Corpse.Open), new Type[] { typeof(Player) })]
    public static void PostOpen(Corpse __instance, Player player)
    {
        try
        {
            if (Cfg == null || !__instance.IsMonster || !__instance.IsOpen) return;
            bool on;
            lock (gate) on = enabled.Contains(player.Guid.Full);
            if (!on) return;

            var moved = 0;
            foreach (var item in __instance.Inventory.Values.Where(Wanted).ToList())
            {
                if (!__instance.TryRemoveFromInventory(item.Guid, out var taken)) continue;
                if (player.TryCreateInInventoryWithNetworking(taken)) moved++;
                else __instance.TryAddToInventory(taken); // pack full or too heavy: put it back
            }
            if (moved > 0)
            {
                Say(player.Session, $"AutoLoot: took {moved} item(s).");
                if (Cfg.CloseCorpse) __instance.Close(player);
            }
        }
        catch (Exception e) { ModManager.Log($"[AutoLoot] {e.Message}"); }
    }
}
