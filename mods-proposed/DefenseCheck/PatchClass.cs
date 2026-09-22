using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace DefenseCheck;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Reply(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // Verified against ACE master, re-fetched for this build:
    // - Source/ACE.Server/WorldObjects/Creature_Combat.cs - public uint GetEffectiveDefenseSkill(CombatType combatType):
    //   "Returns the effective defense skill for a player or creature, ie. with Defender bonus and imbues". Body picks
    //   MeleeDefense or MissileDefense skill by combatType, applies the weapon defense modifier, burden mod, defense
    //   imbues (GetDefenseImbues), and (for Player) the stance mod; zeroes the result if IsExhausted. Confirmed public
    //   instance method on partial class Creature (ACE.Server.WorldObjects), inherited directly by Player. CombatType
    //   is ACE.Entity.Enum.CombatType, already resolvable via the repo's existing GlobalUsings (see AmbushStrike, which
    //   already references CombatType.Melee/.Missile the same way) with values Melee and Missile.
    // - Source/ACE.Server/WorldObjects/Creature_Magic.cs - public uint GetEffectiveMagicDefense(): doc comment "Returns
    //   the creature's effective magic defense skill with item.WeaponMagicDefense and imbues factored in". Body reads
    //   GetCreatureSkill(Skill.MagicDefense).Current, GetWeaponMagicDefenseModifier(this), and
    //   GetDefenseImbues(ImbuedEffectType.MagicDefense). Confirmed public instance method, no parameters, returns uint.
    // Both are pure computed-value getters that ACE's own combat resolver already calls on every incoming attack; this
    // command only reads them, never calls TakeDamage or any combat-resolution path. Neither GetWeaponMagicDefenseModifier
    // nor GetDefenseImbues is called directly by this command - both are already baked into the two public wrapper
    // methods this command calls, matching the pattern BurdenCheck used for GetEncumbranceCapacity/GetAvailableBurden.
    [CommandHandler("defensecheck", AccessLevel.Player, CommandHandlerFlag.None, 0, "Shows your effective melee, missile, and magic defense.")]
    public static void HandleDefenseCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "Defense check is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var meleeDefense = player.GetEffectiveDefenseSkill(CombatType.Melee);
        var missileDefense = player.GetEffectiveDefenseSkill(CombatType.Missile);
        var magicDefense = player.GetEffectiveMagicDefense();

        Reply(session, $"Effective defense - Melee: {meleeDefense}, Missile: {missileDefense}, Magic: {magicDefense}.");
    }
}
