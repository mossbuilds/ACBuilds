using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace RentReminder;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly Dictionary<uint, DateTime> LastSent = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Send(Player p, string text) =>
        p.Session?.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));

    private static string Span(long secs)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, secs));
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h" : $"{t.Hours}h {t.Minutes}m";
    }

    // Own houses only: HouseManager.GetCharacterHouses(playerGuid) (rent queue), GetRentDue(purchaseTime) from the caller's own HousePurchaseTimestamp.
    // Returns seconds until due for the soonest house, or null if none.
    private static long? SecondsUntilDue(Player p, out long dueUnix)
    {
        dueUnix = 0;
        var houses = HouseManager.GetCharacterHouses(p.Guid.Full);
        if (houses == null || houses.Count == 0) return null;
        var purchase = (uint)(p.HousePurchaseTimestamp ?? 0);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long? best = null;
        foreach (var h in houses)
        {
            long due = h.GetRentDue(purchase);
            if (best == null || due - now < best) { best = due - now; dueUnix = due; }
        }
        return best;
    }

    private static void Report(Player p, bool onDemand)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) return;
        var left = SecondsUntilDue(p, out var due);
        if (left == null) { if (onDemand) Send(p, cfg.NoHouseMessage); return; }
        var dueText = DateTimeOffset.FromUnixTimeSeconds(due).UtcDateTime.ToString("yyyy-MM-dd HH:mm");
        bool soon = left.Value < cfg.WarnDays * 86400;
        if (!onDemand)
        {
            if (!soon) return;
            var now = DateTime.UtcNow;
            lock (LastSent)
            {
                if (LastSent.TryGetValue(p.Guid.Full, out var t) && (now - t).TotalMinutes < cfg.CooldownMinutes) return;
                LastSent[p.Guid.Full] = now;
            }
        }
        if (left.Value <= 0) Send(p, cfg.OverdueMessage);
        else Send(p, string.Format(soon ? cfg.DueMessage : cfg.OkMessage, Span(left.Value), dueText));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnter(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || !cfg.WarnAtLogin) return;
            var p = __instance;
            new ActionChain(p, () => { }).AddDelaySeconds(cfg.DelaySeconds).AddAction(p, () =>
            {
                try { Report(p, false); }
                catch (Exception e) { ModManager.Log($"[RentReminder] {e.Message}"); }
            }).EnqueueChain();
        }
        catch (Exception e) { ModManager.Log($"[RentReminder] {e.Message}"); }
    }

    [CommandHandler("rent", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows when your own house rent is due.", "")]
    public static void HandleRent(Session session, params string[] parameters)
    {
        try { Report(session.Player, true); }
        catch (Exception e) { ModManager.Log($"[RentReminder] {e.Message}"); }
    }
}
