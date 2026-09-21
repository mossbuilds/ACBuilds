using ACE.Database;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace ModTest;

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

    // All checks are read-only. Verified against ACE master: ModManager (ACE.Server.Mods, public static) has the public
    // GetModContainerByName(string, bool allowPartial); its Mods list is PRIVATE so it is not used. ModContainer.Status
    // (public field, ModStatus.Active = loaded and running) and .Meta.Version (ModMetadata.Version) are public.
    // CommandManager.GetCommands() as in CommandIndex. DatabaseManager.World (ACE.Database) .GetCachedWeenie(uint) is a
    // ConcurrentDictionary cache read (a miss does one read-only DB query). PropertyManager.GetBool(key, fallback, cacheFallback)
    // (ACE.Server.Managers) returns Property<bool> with public Item.
    [CommandHandler("modtest", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Read-only PASS/FAIL/SKIP self-checks of the custom mods.")]
    public static void HandleModTest(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Reply(session, "ModTest is not enabled."); return; }
        int pass = 0, fail = 0, skip = 0;
        void Line(string status, string text)
        {
            if (status == "PASS") pass++; else if (status == "FAIL") fail++; else skip++;
            Reply(session, $"{status} {text}");
        }

        // (1) mods loaded
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in cfg.ExpectedMods ?? new())
        {
            try
            {
                var c = ModManager.GetModContainerByName(name, false);
                if (c == null) { Line("SKIP", $"mod {name} not loaded"); continue; }
                if (c.Status == ModStatus.Active) { loaded.Add(name); Line("PASS", $"mod {name} v{c.Meta?.Version} active"); }
                else Line("FAIL", $"mod {name} present but {c.Status}");
            }
            catch (Exception) { Line("FAIL", $"mod {name} check errored"); }
        }

        // (2) commands (copy first; the dictionary is not thread-safe)
        HashSet<string>? cmds = null;
        for (var i = 0; i < 3 && cmds == null; i++)
        {
            try { cmds = new HashSet<string>(CommandManager.GetCommands().Where(c => c?.Attribute != null).Select(c => c.Attribute.Command), StringComparer.OrdinalIgnoreCase); }
            catch (InvalidOperationException) { }
        }
        if (cmds == null) Line("FAIL", "command list busy, try again");
        else foreach (var kv in cfg.ExpectedCommands ?? new())
        {
            if (kv.Value == null || kv.Value.Count == 0) continue;
            if (!loaded.Contains(kv.Key)) { Line("SKIP", $"commands of {kv.Key} (mod not active)"); continue; }
            foreach (var cmd in kv.Value)
                Line(cmds.Contains(cmd) ? "PASS" : "FAIL", $"command /{cmd} ({kv.Key})");
        }

        // (3) weenies
        foreach (var wcid in cfg.RequiredWeenies ?? new())
        {
            try { Line(DatabaseManager.World.GetCachedWeenie(wcid) != null ? "PASS" : "FAIL", $"weenie {wcid}"); }
            catch (Exception) { Line("FAIL", $"weenie {wcid} read errored"); }
        }

        // (4) properties readable (values are not printed)
        foreach (var key in cfg.BoolProperties ?? new())
        {
            try { PropertyManager.GetBool(key, false, false); Line("PASS", $"property {key} readable"); }
            catch (Exception) { Line("FAIL", $"property {key} unreadable"); }
        }

        Reply(session, $"ModTest: {pass} PASS, {fail} FAIL, {skip} SKIP");
    }
}
