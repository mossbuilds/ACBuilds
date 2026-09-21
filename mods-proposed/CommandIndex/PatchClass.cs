using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace CommandIndex;

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

    // Verified (ACE master Command/CommandManager.cs, namespace ACE.Server.Command, public static class):
    // GetCommands() returns IEnumerable<CommandHandlerInfo>; .Attribute is CommandHandlerAttribute with
    // Command, Access (AccessLevel), Flags, ParameterCount, Description, Usage. Mods register in the same dictionary.
    // Session.AccessLevel is public (ACE.Server.Network.Session).
    [CommandHandler("modhelp", AccessLevel.Player, CommandHandlerFlag.None, 0, "Lists the extra commands this server's mods add.")]
    public static void HandleModHelp(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "The command list is not available."); return; }

        // Console caller (session null) is treated as the highest level.
        var level = session?.AccessLevel ?? AccessLevel.Developer;

        // The dictionary is not thread-safe; copy it, retrying if a mod registers mid-copy.
        List<CommandHandlerInfo>? all = null;
        for (var i = 0; i < 3 && all == null; i++)
        {
            try { all = CommandManager.GetCommands().ToList(); } catch (InvalidOperationException) { }
        }
        if (all == null) { Reply(session, "The command list is busy, try again."); return; }

        var allow = new HashSet<string>(cfg.Allowlist ?? new(), StringComparer.OrdinalIgnoreCase);
        var lines = all
            .Where(c => c?.Attribute != null && allow.Contains(c.Attribute.Command) && c.Attribute.Access <= level)
            .OrderBy(c => c.Attribute.Command, StringComparer.OrdinalIgnoreCase)
            .Select(c =>
            {
                var text = cfg.DescriptionOverrides != null && cfg.DescriptionOverrides.TryGetValue(c.Attribute.Command, out var o) ? o : c.Attribute.Description;
                return string.IsNullOrWhiteSpace(text) ? $"  /{c.Attribute.Command}" : $"  /{c.Attribute.Command} - {text}";
            })
            .ToList();

        if (lines.Count == 0) { Reply(session, "No extra commands are available."); return; }
        Reply(session, "Server commands:");
        foreach (var l in lines) Reply(session, l);
    }
}
