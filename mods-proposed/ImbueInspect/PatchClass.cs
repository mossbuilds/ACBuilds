using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ImbueInspect;

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

    // Free command name (checked against %TEMP%\cmds.txt, 327 built-ins - no clash): imbuecheck.
    [CommandHandler("imbuecheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Lists which imbued effects are active on your last-appraised weapon, in plain English.")]
    public static void HandleImbueCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ImbueInspect is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Same last-appraised-object pattern as PriceCheck/VendorStock/ItemSetPreview/TinkerHistory.
        // CommandHandlerHelper is `internal static` (ACE.Server.Command.Handlers, verified) and not
        // visible outside ACE.Server, so we reproduce its GetLastAppraisedObject() body directly:
        // Player.RequestedAppraisalTarget (public uint?, Player_Properties.cs) resolved with
        // Player.FindObject(.., SearchLocations.Everywhere, ..) (public).
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise a weapon first (examine it), then try /imbuecheck again."); return; }

        var item = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (item == null) { Say(session, "Couldn't find your last appraised item - it may no longer exist."); return; }

        if (item.WeenieType != WeenieType.MeleeWeapon && item.WeenieType != WeenieType.MissileLauncher
            && item.WeenieType != WeenieType.Missile && item.WeenieType != WeenieType.Caster)
        {
            Say(session, $"{item.Name} isn't a weapon.");
            return;
        }

        ReportImbues(session, item);
    }

    // Named rending effects mapped to the plain damage type they rend, per WorldObject.GetRendDamageType
    // (WorldObject_Weapon.cs, verified). Every other flag reported below with its own plain phrase.
    private static readonly (ImbuedEffectType Flag, string Label)[] RendingEffects =
    [
        (ImbuedEffectType.SlashRending, "slash"),
        (ImbuedEffectType.PierceRending, "pierce"),
        (ImbuedEffectType.BludgeonRending, "bludgeon"),
        (ImbuedEffectType.FireRending, "fire"),
        (ImbuedEffectType.ColdRending, "cold"),
        (ImbuedEffectType.AcidRending, "acid"),
        (ImbuedEffectType.ElectricRending, "electric"),
        (ImbuedEffectType.NetherRending, "nether"),
    ];

    // GetImbuedEffects() (public instance method, WorldObject_Weapon.cs, verified) returns the
    // bitwise-OR of the five PropertyInt-backed imbue slots (ImbuedEffect..ImbuedEffect5). We check
    // this result directly with HasFlag rather than calling the separate public instance method
    // WorldObject.HasImbuedEffect(ImbuedEffectType) - that method only reads the single ImbuedEffect
    // property (slot 1, see the ImbuedEffect property in WorldObject_Properties.cs), not the
    // combined result GetImbuedEffects() computes, so it would silently miss anything imbued into
    // slots 2-5. Both methods are genuinely public; this is a real gap between them, not an
    // accessibility issue, and using GetImbuedEffects() + our own HasFlag check is the only way to
    // report every active slot honestly.
    private static void ReportImbues(Session session, WorldObject weapon)
    {
        Say(session, $"--- {weapon.Name} ---");

        var effects = weapon.GetImbuedEffects();

        if (effects == ImbuedEffectType.Undef)
        {
            Say(session, "No imbued effects are active on this weapon.");
            return;
        }

        if (effects.HasFlag(ImbuedEffectType.CriticalStrike))
            Say(session, "Critical Strike: increased chance to land a critical hit.");

        if (effects.HasFlag(ImbuedEffectType.CripplingBlow))
            Say(session, "Crippling Blow: critical hits deal extra damage.");

        if (effects.HasFlag(ImbuedEffectType.ArmorRending))
            Say(session, "Armor Rending: reduces the target's armor level on hit.");

        foreach (var (flag, label) in RendingEffects)
        {
            if (effects.HasFlag(flag))
                Say(session, $"{label[0].ToString().ToUpperInvariant()}{label[1..]} Rending: boosts damage against {label}-vulnerable resistances.");
        }

        if (effects.HasFlag(ImbuedEffectType.MeleeDefense))
            Say(session, "Melee Defense: improved defense against melee attacks.");

        if (effects.HasFlag(ImbuedEffectType.MissileDefense))
            Say(session, "Missile Defense: improved defense against missile attacks.");

        if (effects.HasFlag(ImbuedEffectType.MagicDefense))
            Say(session, "Magic Defense: improved defense against magic attacks.");

        if (effects.HasFlag(ImbuedEffectType.Spellbook))
            Say(session, "Spellbook: casts an associated spell (item-specific).");

        if (effects.HasFlag(ImbuedEffectType.IgnoreSomeMagicProjectileDamage))
            Say(session, "Ignores a portion of incoming magic projectile damage.");

        if (effects.HasFlag(ImbuedEffectType.AlwaysCritical))
            Say(session, "Always Critical: every hit lands as a critical.");

        if (effects.HasFlag(ImbuedEffectType.IgnoreAllArmor))
            Say(session, "Ignore All Armor: bypasses the target's armor entirely.");

        Say(session, "Note: this lists which effects are active, not their exact numeric strength (rend %, crit-rate bonus).");
    }
}
