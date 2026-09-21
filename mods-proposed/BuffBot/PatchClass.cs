using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace BuffBot;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly Dictionary<uint, DateTime> lastUse = new();
    private static readonly object gate = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        return base.OnWorldOpen();
    }

    private static void Say(Session s, string msg) =>
        s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // /buffme is not a built-in ACE command name.
    [CommandHandler("buffme", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Cast a standard set of self buffs (cooldown applies).", "")]
    public static void HandleBuffMe(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "BuffBot is switched off."); return; }
        var p = session.Player;
        lock (gate)
        {
            if (lastUse.TryGetValue(p.Guid.Full, out var t))
            {
                var left = cfg.CooldownSeconds - (DateTime.UtcNow - t).TotalSeconds;
                if (left > 0) { Say(session, $"Buffs ready again in {(int)left}s."); return; }
            }
            lastUse[p.Guid.Full] = DateTime.UtcNow;
        }
        var n = 0;
        foreach (var name in cfg.Spells)
        {
            if (!Enum.TryParse<SpellId>(name, out var id)) continue;
            // WorldObject.TryCastSpell(Spell, WorldObject target, ..., tryResist) verified in WorldObject_Magic.cs
            p.TryCastSpell(new Spell(id), p, tryResist: false);
            n++;
        }
        Say(session, $"BuffBot: cast {n} buff(s).");
    }
}
