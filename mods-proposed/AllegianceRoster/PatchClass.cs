using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AllegianceRoster;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // "roster" is not a built-in ACE command (checked against the built-in list in %TEMP%\cmds.txt).
    // Player-only, own allegiance only: no target-player/admin variant. Read-only, no files, no world objects.
    [CommandHandler("roster", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows your own allegiance roster.", "[all [page] | vassals]")]
    public static void HandleRoster(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "AllegianceRoster is switched off."); return; }

        try
        {
            // AllegianceManager.GetAllegianceNode(IPlayer) is public static (ACE.Server.Managers, verified);
            // Player implements IPlayer (ACE.Server.Entity). Returns null if the caller has no allegiance.
            var node = AllegianceManager.GetAllegianceNode(p);
            if (node == null) { Say(session, "You are not in an allegiance."); return; }

            var allegiance = node.Allegiance;
            var monarchName = SafeName(node.Monarch?.Player);
            var patronName = node.Patron != null ? SafeName(node.Patron.Player) : "(none - you are the monarch)";

            var sub = parameters.Length > 0 ? parameters[0].ToLowerInvariant() : "";

            if (sub == "vassals")
            {
                Say(session, $"--- Your vassals ({node.TotalVassals}) ---");
                if (node.TotalVassals == 0) { Say(session, "You have no vassals."); return; }
                foreach (var v in node.Vassals.Values)
                    Say(session, FormatMember(v));
                return;
            }

            if (sub == "all")
            {
                // Allegiance.Members is the full member dictionary for this allegiance (guid -> AllegianceNode, verified in Player_Allegiance.cs).
                var all = allegiance.Members.Values
                    .Where(n => cfg.ShowOffline || PlayerManager.GetOnlinePlayer(n.PlayerGuid) != null)
                    .OrderByDescending(n => PlayerManager.GetOnlinePlayer(n.PlayerGuid) != null)
                    .ThenBy(n => SafeName(n.Player))
                    .ToList();

                int page = 1;
                if (parameters.Length > 1 && int.TryParse(parameters[1], out var pg)) page = Math.Max(1, pg);
                var perPage = Math.Max(1, cfg.MaxLines);
                var totalPages = Math.Max(1, (all.Count + perPage - 1) / perPage);
                page = Math.Min(page, totalPages);

                Say(session, $"--- Allegiance roster: {all.Count} member(s), page {page}/{totalPages} ---");
                foreach (var n in all.Skip((page - 1) * perPage).Take(perPage))
                    Say(session, FormatMember(n));
                if (totalPages > 1) Say(session, $"Type /roster all {page + 1} for the next page.");
                return;
            }

            // Default: summary + online members only.
            Say(session, $"--- Your allegiance: monarch {monarchName}, patron {patronName} ---");
            Say(session, $"Members: {allegiance.TotalMembers}, vassals under you: {node.TotalVassals}");

            var online = allegiance.Members.Values
                .Select(n => PlayerManager.GetOnlinePlayer(n.PlayerGuid))
                .Where(op => op != null)
                .OrderBy(op => op!.Name)
                .ToList();

            Say(session, $"Online now ({online.Count}):");
            foreach (var op in online)
                Say(session, $"  {op!.Name} - level {op.Level ?? 1}");
            if (online.Count == 0) Say(session, "  (nobody)");
        }
        catch (Exception e)
        {
            ModManager.Log($"[AllegianceRoster] {e.Message}");
        }
    }

    // IPlayer.Name/Level are on the interface (ACE.Server.Entity.IPlayer, verified in IPlayer.cs); works for
    // both online (Player) and offline (OfflinePlayer) members. No last-login field is exposed on IPlayer,
    // so only level and online/offline are shown - no last-seen timestamp, per the idea's privacy note.
    private static string FormatMember(AllegianceNode n)
    {
        var ip = n.Player;
        var name = SafeName(ip);
        var level = ip?.Level ?? 0;
        var online = PlayerManager.GetOnlinePlayer(n.PlayerGuid) != null;
        return $"  {name} - level {level} - {(online ? "online" : "offline")} - rank {n.Rank}";
    }

    private static string SafeName(IPlayer? p) => p?.Name ?? "(unknown)";
}
