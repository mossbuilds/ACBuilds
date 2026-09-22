using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace SpellFizzleForecast;

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

    [CommandHandler("fizzlecheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
        "Previews your fizzle chance for a spell, using the real fizzle-chance formula, without casting anything.",
        "<spellId>")]
    public static void HandleFizzleCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "SpellFizzleForecast is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        if (parameters.Length == 0 || !uint.TryParse(parameters[0], out var spellId))
        {
            Say(session, "Usage: /fizzlecheck <spellId> - the numeric spell ID, e.g. the one shown by @spellinfo or a spell reference.");
            return;
        }

        // Spell(uint, bool loadDB = true) is a public constructor (Entity/Spell.cs, verified). NotFound
        // (public bool, verified) is true when the DAT-file spell table has no entry for this ID - this
        // never touches the player or any item, it only loads static spell definition data.
        var spell = new Spell(spellId);
        if (spell.NotFound)
        {
            Say(session, $"No spell found for spell ID {spellId}.");
            return;
        }

        // Spell.School (public MagicSchool, get => _spellBase.School) and Spell.GetMagicSkill() (public
        // Skill GetMagicSkill(), a pure switch on School) are both verified pure reads of DAT-loaded
        // spell data (Entity/SpellProperties.cs, Entity/Spell.cs). This is the same skill-resolution
        // ACE's own real cast path uses in Player_Magic.cs's GetCastingPreCheckStatus callers
        // (`GetCreatureSkill(spell.School).Current`) - Spell.GetMagicSkill() resolves the same School to
        // a Skill enum instead, then Creature.GetCreatureSkill(Skill, bool add = true) (public,
        // Creature_Skills.cs, verified) is used directly, matching what GetCreatureSkill(MagicSchool)
        // does internally.
        var magicSkill = spell.GetMagicSkill();
        var skillLevel = player.GetCreatureSkill(magicSkill).Current;

        // Spell.Power (public uint, get => _spellBase.Power) is the exact "difficulty" value the real
        // cast path reads (`var difficulty = spell.Power;` in Player_Magic.cs's
        // GetCastingPreCheckStatus, verified this round by direct fetch).
        var difficulty = spell.Power;

        // SkillCheck.GetMagicSkillChance(int skill, int difficulty) is public static
        // (WorldObjects/SkillCheck.cs, verified this round by direct fetch, full file): a two-line
        // wrapper `return GetSkillChance(skill, difficulty, 0.07f);`, itself public static and pure -
        // both are simple math over their arguments (a logistic curve) with no field reads/writes, no
        // RNG call, and no side effects. This is the EXACT function the real cast path calls
        // (`SkillCheck.GetMagicSkillChance((int)magicSkill, (int)difficulty)` in
        // GetCastingPreCheckStatus) to compute the success chance, before that method separately rolls a
        // random number against it and fires the visible fizzle animation/message - neither of which
        // this command ever touches.
        var successChance = SkillCheck.GetMagicSkillChance((int)skillLevel, (int)difficulty);
        var fizzleChance = 1.0 - successChance;

        Say(session, $"--- Fizzle forecast: {spell.Name} (id {spell.Id}) ---");
        Say(session, $"Your {magicSkill} skill: {skillLevel}  |  Spell power (difficulty): {difficulty}");
        Say(session, $"Estimated fizzle chance: {fizzleChance * 100.0:0.0}%  (success chance: {successChance * 100.0:0.0}%)");
        Say(session, "This is a preview only - nothing was cast, and no roll was made.");
    }
}
