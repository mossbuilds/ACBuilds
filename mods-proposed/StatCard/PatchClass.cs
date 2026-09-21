using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace StatCard;

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

    // Player-only command: shows the caller's own data. Runs on the caller's own session thread.
    [CommandHandler("statcard", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows a summary of your character.", "[top]")]
    public static void HandleStatCard(Session session, params string[] parameters)
    {
        var p = session?.Player;
        if (p == null) return;
        var cfg = Cfg ?? new Settings();
        int top = Math.Max(1, cfg.TopSkills);
        if (parameters.Length > 0 && int.TryParse(parameters[0], out var n)) top = n;
        top = Math.Clamp(top, 1, Math.Max(1, cfg.MaxTopSkills));

        Say(session, $"--- {p.Name}, level {p.Level ?? 1} ---");
        if (p.IsMaxLevel)
            Say(session, "XP: max level reached.");
        else
            Say(session, $"XP: {p.TotalExperience ?? 0:N0} total, {p.GetRemainingXP():N0} to next level. Unspent XP: {p.AvailableExperience ?? 0:N0}.");
        var lum = p.AvailableLuminance ?? 0;
        Say(session, $"Skill credits: {p.AvailableSkillCredits ?? 0}" + (lum > 0 ? $", luminance: {lum:N0}" : ""));
        Say(session, $"Attributes: Str {p.Strength.Current}, End {p.Endurance.Current}, Coord {p.Coordination.Current}, Quick {p.Quickness.Current}, Focus {p.Focus.Current}, Self {p.Self.Current}");
        Say(session, $"Vitals: Health {p.Health.MaxValue}, Stamina {p.Stamina.MaxValue}, Mana {p.Mana.MaxValue}");

        var skills = p.Skills.Values
            .Where(s => s.AdvancementClass >= SkillAdvancementClass.Trained)
            .OrderByDescending(s => s.Current)
            .Take(top);
        foreach (var s in skills)
            Say(session, $"  {s.Skill}: {s.Current} ({(s.AdvancementClass == SkillAdvancementClass.Specialized ? "specialized" : "trained")})");

        if (cfg.ShowDeaths) Say(session, $"Deaths: {p.NumDeaths}");
    }
}
