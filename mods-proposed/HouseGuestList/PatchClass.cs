using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace HouseGuestList;

// Read-only. No ace_auth/ace_shard access, no world objects, no Harmony patches needed - a pure
// command handler plus reads of House.Guests / House.StorageAccess (both public, ACE.Server.WorldObjects,
// confirmed against raw.githubusercontent.com/ACEmulator/ACE/master/Source/ACE.Server/WorldObjects/House.cs)
// and HouseManager.GetCharacterHouses (public static, ACE.Server.Managers, returns List<House> - confirmed
// against .../Source/ACE.Server/Managers/HouseManager.cs). "myguests" is not a built-in ACE command
// (checked against the built-in list in %TEMP%\cmds.txt: raise/unfreeze/resyncproperties are built-ins, order is not).
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

    // Resolves a guest's guid to a name, same lookup pattern RentReminder/AllegianceRoster use via
    // PlayerManager. FindByGuid(uint) covers online and offline characters; if the character has since
    // been deleted (or the lookup otherwise fails) this returns null and the caller prints the guid instead
    // of erroring, per the idea's stated risk.
    private static string? ResolveName(ObjectGuid guid)
    {
        try
        {
            return PlayerManager.FindByGuid(guid.Full)?.Name;
        }
        catch
        {
            return null;
        }
    }

    [CommandHandler("myguests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows who is on your own house's guest list right now, and whether they have storage access.", "")]
    public static void HandleMyGuests(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "HouseGuestList is switched off."); return; }

        try
        {
            // Own houses only: HouseManager.GetCharacterHouses(playerGuid), same call RentReminder uses.
            var houses = HouseManager.GetCharacterHouses(p.Guid.Full);
            if (houses == null || houses.Count == 0) { Say(session, "You don't own a house."); return; }

            foreach (var house in houses)
            {
                var guests = house.Guests;
                if (guests == null || guests.Count == 0)
                {
                    Say(session, houses.Count > 1
                        ? $"{house.Name}: guest list is empty."
                        : "Your guest list is empty.");
                    continue;
                }

                if (houses.Count > 1) Say(session, $"{house.Name} guest list ({guests.Count}):");
                else Say(session, $"Your guest list ({guests.Count}):");

                foreach (var kvp in guests)
                {
                    var guid = kvp.Key;
                    var hasStorage = kvp.Value;
                    var who = ResolveName(guid) ?? $"guid 0x{guid.Full:X8}";
                    Say(session, $"  {who} - {(hasStorage ? "storage access" : "visit only")}");
                }
            }
        }
        catch (Exception e)
        {
            ModManager.Log($"[HouseGuestList] {e.Message}");
        }
    }
}
