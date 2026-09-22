using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace HouseHookMeter;

// Read-only. No ace_auth/ace_shard access, no world objects touched, no Harmony patches needed - a pure
// command handler plus reads of House.HouseMaxHooksUsable / House.HouseCurrentHooksUsable / House.Hooks
// (all public, ACE.Server.WorldObjects, confirmed against raw.githubusercontent.com/ACEmulator/ACE/master/
// Source/ACE.Server/WorldObjects/House.cs) and HouseManager.GetCharacterHouses (public static,
// ACE.Server.Managers, returns List<House> - confirmed against .../Source/ACE.Server/Managers/HouseManager.cs,
// same call HouseGuestList already uses). "myhooks" is not a built-in ACE command (checked against the
// built-in list in %TEMP%\cmds.txt: raise/unfreeze/resyncproperties are built-ins, myguests/myhooks are not).
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

    [CommandHandler("myhooks", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows how many hooks your house has used vs. its cap, and whether the server enforces that cap.", "")]
    public static void HandleMyHooks(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "HouseHookMeter is switched off."); return; }

        try
        {
            // Own houses only: HouseManager.GetCharacterHouses(playerGuid), same call HouseGuestList uses.
            var houses = HouseManager.GetCharacterHouses(p.Guid.Full);
            if (houses == null || houses.Count == 0) { Say(session, "You don't own a house."); return; }

            // house_hook_limit is a global on/off switch for the whole limit (Server-Configurable-Options wiki:
            // "if disabled, house hook limits are ignored", default true). Same PropertyManager.GetBool pattern
            // SettingsPeek ships; read once and say plainly when the cap below isn't actually enforced, per the
            // idea's stated risk, rather than printing a number that means nothing.
            var limitEnforced = PropertyManager.GetBool("house_hook_limit", true, false).Item;

            foreach (var house in houses)
            {
                var used = house.HouseCurrentHooksUsable;
                var cap = house.HouseMaxHooksUsable;
                var physical = house.Hooks?.Count ?? 0;

                var prefix = houses.Count > 1 ? $"{house.Name}: " : "";
                Say(session, limitEnforced
                    ? $"{prefix}hooks in use: {used} / {cap} usable"
                    : $"{prefix}hooks in use: {used} (cap {cap} usable, but house_hook_limit is off on this server - the cap is not enforced)");
                Say(session, $"{prefix}hook fixtures physically present: {physical}");
            }
        }
        catch (Exception e)
        {
            ModManager.Log($"[HouseHookMeter] {e.Message}");
        }
    }
}
