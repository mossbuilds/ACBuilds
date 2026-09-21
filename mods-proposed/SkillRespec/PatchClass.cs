using System.Text.Json;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace SkillRespec;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const uint PyrealWcid = 273; // ACE coinStackWcid
    private static Settings? Cfg;
    private static readonly object gate = new();
    private static Dictionary<uint, double> lastUse = new();      // player guid -> unix time
    private static Dictionary<uint, (Skill skill, double at)> pending = new();

    private static double Now() => ACE.Common.Time.GetUnixTime();

    private static void Save() // under gate
    {
        try { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(lastUse)); }
        catch (Exception e) { ModManager.Log($"[SkillRespec] save: {e.Message}"); }
    }

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try
        {
            if (File.Exists(Cfg.DataFile))
                lock (gate) lastUse = JsonSerializer.Deserialize<Dictionary<uint, double>>(File.ReadAllText(Cfg.DataFile)) ?? new();
        }
        catch (Exception e) { ModManager.Log($"[SkillRespec] load: {e.Message}"); }
        return base.OnWorldOpen();
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    [CommandHandler("respec", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
        "Resets one skill and refunds its spent XP (and the specialization credits).",
        "<skill> [confirm]  - repeat the command or add 'confirm' within the window to proceed")]
    public static void HandleRespec(Session session, params string[] parameters)
    {
        var cfg = Cfg ?? new Settings();
        var p = session?.Player;
        if (p == null) return;
        if (!cfg.Enabled) { Say(session, "Skill respec is not enabled on this server."); return; }
        if (!Enum.TryParse<Skill>(parameters[0], true, out var skill) || !Player.PlayerSkills.Contains(skill))
        { Say(session, $"Unknown skill {parameters[0]}. Use the skill name without spaces, e.g. HealingSkill or TwoHandedCombat."); return; }
        if (p.CombatMode != CombatMode.NonCombat || p.PKTimerActive)
        { Say(session, "You cannot respec while in combat or with an active PK timer."); return; }
        var cs = p.GetCreatureSkill(skill, false);
        if (cs == null || cs.AdvancementClass < SkillAdvancementClass.Trained)
        { Say(session, "That skill is not trained or specialized; nothing to reset."); return; }

        var guid = p.Guid.Full;
        var now = Now();
        bool confirmed = parameters.Length > 1 && parameters[1].Equals("confirm", StringComparison.OrdinalIgnoreCase);
        lock (gate)
        {
            if (lastUse.TryGetValue(guid, out var t) && now - t < cfg.CooldownHours * 3600.0)
            { Say(session, $"Respec on cooldown: {(int)((cfg.CooldownHours * 3600.0 - (now - t)) / 3600) + 1} hour(s) left."); return; }
            if (!confirmed && pending.TryGetValue(guid, out var pe) && pe.skill == skill && now - pe.at <= cfg.ConfirmSeconds)
                confirmed = true;
            if (!confirmed)
            {
                pending[guid] = (skill, now);
                Say(session, $"This resets {skill} to 0 ranks, refunds its XP" +
                    (cs.AdvancementClass == SkillAdvancementClass.Specialized ? " and returns the specialization credits" : "") +
                    (cfg.CostPyreals > 0 ? $", and costs {cfg.CostPyreals} pyreals" : "") +
                    $". Type /respec {skill} confirm within {cfg.ConfirmSeconds}s to proceed.");
                return;
            }
            pending.Remove(guid);
        }

        // Run on the player's own actor; ResetSkill sends the client updates itself.
        new ActionChain(p, () =>
        {
            try
            {
                if (p.CombatMode != CombatMode.NonCombat || p.PKTimerActive) { Say(session, "Respec cancelled: in combat."); return; }
                // Never charge for a skill that cannot be reset (ResetSkill only works on trained/specialized skills).
                var adv = p.GetCreatureSkill(skill).AdvancementClass;
                if (adv != SkillAdvancementClass.Trained && adv != SkillAdvancementClass.Specialized)
                { Say(session, "That skill is not trained, so there is nothing to reset."); return; }
                if (cfg.CostPyreals > 0)
                {
                    if (p.GetNumInventoryItemsOfWCID(PyrealWcid) < cfg.CostPyreals)
                    { Say(session, $"You need {cfg.CostPyreals} pyreals in your pack."); return; }
                    if (!p.TryConsumeFromInventoryWithNetworking(PyrealWcid, cfg.CostPyreals))
                    { Say(session, "Could not take the pyreals; respec cancelled."); return; }
                }
                if (!p.ResetSkill(skill, true)) { Say(session, "That skill cannot be reset."); return; }
                lock (gate) { lastUse[guid] = Now(); Save(); }
                p.SaveBiotaToDatabase(); // ACE's normal character save
                ModManager.Log($"[SkillRespec] {p.Name} reset {skill}");
            }
            catch (Exception e) { ModManager.Log($"[SkillRespec] {e.Message}"); }
        }).EnqueueChain();
    }
}
