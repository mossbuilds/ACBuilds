using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AllegianceOfficers;

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

    // "officers" is not a built-in ACE command (checked against the built-in list captured in %TEMP%\cmds.txt
    // for this task). Distinct from the already-shipped AllegianceRoster, which walks the whole member tree by
    // rank number; this reads only the Officers dictionary (Speaker/Seneschal/Castellan) and their effective
    // titles. Player-only, own allegiance only. Read-only, no patches, no world objects.
    [CommandHandler("officers", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Lists your allegiance's officers and their titles.")]
    public static void HandleOfficers(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "AllegianceOfficers is switched off."); return; }

        try
        {
            // AllegianceManager.GetAllegiance(IPlayer) is public static (ACE.Server.Managers, verified via
            // github-second-brain direct fetch of Managers/AllegianceManager.cs); Player implements IPlayer
            // (ACE.Server.Entity). Returns null if the allegiance biota can't be resolved.
            var allegiance = AllegianceManager.GetAllegiance(p);
            if (allegiance == null) { Say(session, "You are not in an allegiance."); return; }

            // Allegiance.Officers (public Dictionary<ObjectGuid, AllegianceNode>, WorldObjects/Allegiance.cs,
            // verified via direct fetch) is built by BuildOfficers(): Members filtered on the also-public
            // Player.AllegianceOfficerRank != null (WorldObjects/Player_Allegiance.cs, `public int?
            // AllegianceOfficerRank { get; set; }`-style property - the round's flagged caveat is resolved:
            // it is a plain public property, confirmed by direct fetch, no reflection needed).
            var officers = allegiance.Officers;
            if (officers == null || officers.Count == 0)
            {
                Say(session, "This allegiance has no officers.");
                return;
            }

            Say(session, $"--- Allegiance officers ({officers.Count}) ---");

            foreach (var kvp in officers.Values.OrderByDescending(n => n.Player?.AllegianceOfficerRank ?? 0))
            {
                var member = kvp.Player;
                var name = member?.Name ?? "(unknown)";
                var rank = member?.AllegianceOfficerRank;

                // AllegianceOfficerLevel (ACE.Entity.Enum, via the AllegianceOfficerRank int) and
                // Allegiance.GetOfficerTitle(AllegianceOfficerLevel) (public, WorldObjects/Allegiance.cs,
                // verified) fall back to "Speaker"/"Seneschal"/"Castellan" when no custom title is set.
                string title;
                if (rank.HasValue && Enum.IsDefined(typeof(AllegianceOfficerLevel), rank.Value))
                    title = allegiance.GetOfficerTitle((AllegianceOfficerLevel)rank.Value);
                else
                    title = "(unknown rank)";

                var online = PlayerManager.GetOnlinePlayer(kvp.PlayerGuid) != null;
                Say(session, $"  {title}: {name} - rank {rank?.ToString() ?? "?"} - {(online ? "online" : "offline")}");
            }

            if (allegiance.HasCustomTitles)
                Say(session, "(one or more titles above are custom, set by the monarch)");
        }
        catch (Exception e)
        {
            ModManager.Log($"[AllegianceOfficers] {e.Message}");
        }
    }
}
