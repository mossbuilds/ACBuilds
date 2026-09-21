using ACE.Server.Network;
using ACE.Shared.Mods;

namespace AdminAudit;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    // CommandManager.GetCommandHandler(Session, string, string[], out CommandHandlerInfo) is public static in ACE.Server.Command;
    // GameActionTalk calls it for every in-game @command. Response Ok/SudoOk means the command is about to run (sudo already unwrapped into commandInfo).
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CommandManager), nameof(CommandManager.GetCommandHandler),
        new Type[] { typeof(Session), typeof(string), typeof(string[]), typeof(CommandHandlerInfo) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    public static void PostGetHandler(CommandHandlerResponse __result, Session session, string command, string[] parameters, CommandHandlerInfo commandInfo)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled || session == null || commandInfo == null) return;
            if (__result != CommandHandlerResponse.Ok && __result != CommandHandlerResponse.SudoOk) return;
            if (commandInfo.Attribute.Access < cfg.MinLevel) return;

            var name = commandInfo.Attribute.Command;
            var args = parameters ?? Array.Empty<string>();
            if (__result == CommandHandlerResponse.SudoOk && args.Length > 0) args = args.Skip(1).ToArray();
            var shown = cfg.RedactArgs.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase)) ? "(args hidden)" : string.Join(' ', args);
            var who = session.Player?.Name ?? "?";
            var line = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z | {who} | {session.AccessLevel} | {(__result == CommandHandlerResponse.SudoOk ? "sudo " : "")}/{name} {shown}";
            lock (gate) File.AppendAllText(cfg.LogFile, line + Environment.NewLine);
            ModManager.Log($"[AdminAudit] {line}");
        }
        catch (Exception e) { ModManager.Log($"[AdminAudit] {e.Message}"); }
    }
}
