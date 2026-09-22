using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AllegianceBanRoster;

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

    // PlayerManager.FindByGuid(uint) covers online and offline characters; falls back to the raw guid
    // for a deleted character, same pattern as HouseGuestList.
    private static string NameOrGuid(uint guid)
    {
        var name = PlayerManager.FindByGuid(guid)?.Name;
        return name ?? $"(deleted character, guid {guid})";
    }

    // "allegianceban" is not a built-in ACE command (checked against the built-in list captured in
    // %TEMP%\cmds.txt for this task, 327 entries, no match). Player-only, monarch-of-own-allegiance
    // only (checked in-command below - stock allegiance commands are all Admin- or Sentinel-gated by
    // rank elsewhere, but this mod follows the sibling AllegianceRoster/AllegianceOfficers convention
    // of AccessLevel.Player plus its own equality check). Read-only: never adds or removes a ban or an
    // approved vassal - use the stock ban/unban commands for that.
    [CommandHandler("allegianceban", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Monarch-only: lists your allegiance's ban list and pre-approved-vassal list.")]
    public static void HandleAllegianceBan(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "AllegianceBanRoster is switched off."); return; }

        try
        {
            // AllegianceManager.GetAllegiance(IPlayer) is public static (ACE.Server.Managers,
            // verified via direct fetch of Managers/AllegianceManager.cs, same call AllegianceOfficers
            // already uses); Player implements IPlayer (ACE.Server.Entity). Returns null if the caller
            // has no allegiance or the biota can't be resolved.
            var allegiance = AllegianceManager.GetAllegiance(p);
            if (allegiance == null) { Say(session, "You are not in an allegiance."); return; }

            // Monarch-only gate: Allegiance.Monarch is a public AllegianceNode field (WorldObjects/Allegiance.cs,
            // verified by direct fetch); AllegianceNode.PlayerGuid is the same ObjectGuid Allegiance.Equals
            // compares by .Full. p.Guid is the caller's own ObjectGuid.
            if (allegiance.Monarch?.PlayerGuid.Full != p.Guid.Full)
            {
                Say(session, "Only the monarch of your allegiance can view its ban and approved-vassal lists.");
                return;
            }

            // Allegiance.BanList / Allegiance.ApprovedVassals: public Dictionary<uint, PropertiesAllegiance>
            // (WorldObjects/Allegiance.cs, verified by direct fetch this run - `public Dictionary<uint,
            // PropertiesAllegiance> BanList => Biota.PropertiesAllegiance.GetBanList(BiotaDatabaseLock);` and
            // the ApprovedVassals equivalent). Allegiance.IsBanned(uint)/HasApprovedVassal(uint) are also public
            // and confirmed, though this command reads the dictionaries directly rather than re-querying per key.
            //
            // Round-flagged caveat, now resolved: PropertiesAllegiance (ACE.Entity.Models.PropertiesAllegiance,
            // fetched in full this run) is `public class PropertiesAllegiance { public bool Banned { get; set; }
            // public bool ApprovedVassal { get; set; } }` - both fields are plain public auto-properties, no
            // reflection fallback needed. In practice every entry in BanList already has Banned == true (it is
            // how GetBanList filters) and every entry in ApprovedVassals already has ApprovedVassal == true, so
            // printing the per-entry field adds no information beyond which dictionary the guid came from; this
            // command labels each guid by its dictionary instead of re-printing the redundant flag.
            var banList = allegiance.BanList;
            var approved = allegiance.ApprovedVassals;

            Say(session, $"--- Allegiance ban list ({banList?.Count ?? 0}) ---");
            if (banList == null || banList.Count == 0)
                Say(session, "  (no one is banned)");
            else
                foreach (var guid in banList.Keys)
                    Say(session, $"  {NameOrGuid(guid)}");

            Say(session, $"--- Pre-approved vassals ({approved?.Count ?? 0}) ---");
            if (approved == null || approved.Count == 0)
                Say(session, "  (no one is pre-approved)");
            else
                foreach (var guid in approved.Keys)
                    Say(session, $"  {NameOrGuid(guid)}");
        }
        catch (Exception e)
        {
            ModManager.Log($"[AllegianceBanRoster] {e.Message}");
        }
    }
}
