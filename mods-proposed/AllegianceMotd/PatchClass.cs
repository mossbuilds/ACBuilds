using System.Text;
using System.Text.Json;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace AllegianceMotd;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static readonly object gate = new();
    // key: monarch's character guid (ObjectGuid.Full, stable identity of an allegiance) -> MOTD text.
    private static Dictionary<uint, string> motds = new();

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        try
        {
            if (File.Exists(Cfg.DataFile))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<uint, string>>(File.ReadAllText(Cfg.DataFile));
                if (loaded != null) lock (gate) motds = loaded;
            }
        }
        catch (Exception e) { ModManager.Log($"[AllegianceMotd] load: {e.Message}"); }
        return base.OnWorldOpen();
    }

    private static void Save() // call under gate
    {
        try { if (Cfg != null) File.WriteAllText(Cfg.DataFile, JsonSerializer.Serialize(motds)); }
        catch (Exception e) { ModManager.Log($"[AllegianceMotd] save: {e.Message}"); }
    }

    private static void Say(Session session, string msg) =>
        session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

    // Strips control characters and clamps length; a MOTD is chat text, never a command or markup carrier.
    private static string Sanitize(string text, int maxLength)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsControl(c)) continue;
            sb.Append(c);
        }
        var clean = sb.ToString().Trim();
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }

    // AllegianceManager.GetAllegianceNode(IPlayer) is public static (ACE.Server.Managers, verified in
    // AllegianceRoster). Player implements IPlayer (ACE.Server.Entity). Returns null if the caller has no
    // allegiance. AllegianceNode.Monarch is the root node of the caller's own allegiance (readonly,
    // AllegianceNode.cs); AllegianceNode.IsMonarch is "Patron == null" (verified, AllegianceNode.cs line 23) -
    // used here as the permission check instead of comparing ranks, per the idea's own suggestion.
    private static AllegianceNode? MyNode(Player p) => AllegianceManager.GetAllegianceNode(p);

    // The monarch's own node has Monarch == null on some ACE builds mid-construction, but by the time a player
    // is logged in and this is called, AllegianceNode.Monarch is set to the node itself for the monarch (root
    // of Walk()); fall back to the node's own guid when Monarch is null so a lone monarch (no vassals yet)
    // still gets a stable key.
    private static uint MonarchGuid(AllegianceNode node) => (node.Monarch ?? node).PlayerGuid.Full;

    [CommandHandler("amotd", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Shows or sets your allegiance's message of the day.", "[set <text>]")]
    public static void HandleAmotd(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        var p = session?.Player;
        if (p == null) return;
        if (cfg == null || !cfg.Enabled) { Say(session, "AllegianceMotd is switched off."); return; }

        try
        {
            var node = MyNode(p);
            if (node == null) { Say(session, "You are not in an allegiance."); return; }
            var key = MonarchGuid(node);

            if (parameters.Length > 0 && parameters[0].Equals("set", StringComparison.OrdinalIgnoreCase))
            {
                // Monarch or Patron only. node.IsMonarch covers the monarch; a Patron is any node with vassals
                // of its own directly under the monarch chain - but the idea specifically asks for
                // "Monarch OR Patron", i.e. anyone with at least one vassal reporting to them, not just the
                // root. HasVassals (verified, AllegianceNode.cs) covers both: the monarch always has vassals
                // once anyone has sworn in, and a mid-chain patron has their own vassals underneath them.
                if (!node.IsMonarch && !node.HasVassals)
                {
                    Say(session, "Only your allegiance's monarch or a patron may set the MOTD.");
                    return;
                }

                var text = string.Join(" ", parameters.Skip(1)).Trim();
                if (text.Length == 0) { Say(session, "Usage: /amotd set <text>"); return; }
                var clean = Sanitize(text, cfg.MaxLength);
                if (clean.Length == 0) { Say(session, "That message is empty once control characters are stripped."); return; }

                lock (gate) { motds[key] = clean; Save(); }
                Say(session, $"Allegiance MOTD set: {clean}");

                // Reach members already online too, per the idea's own risk note - not just future logins.
                node.Walk(n =>
                {
                    var op = PlayerManager.GetOnlinePlayer(n.PlayerGuid);
                    if (op?.Session != null && op != p)
                        Say(op.Session, $"[Allegiance MOTD] {clean}");
                });
                return;
            }

            string? motd;
            lock (gate) motds.TryGetValue(key, out motd);
            Say(session, string.IsNullOrEmpty(motd) ? "Your allegiance has no message of the day set." : $"[Allegiance MOTD] {motd}");
        }
        catch (Exception e)
        {
            ModManager.Log($"[AllegianceMotd] {e.Message}");
        }
    }

    // Player.PlayerEnterWorld verified in Player_Networking.cs (used the same way in LoginGreeter/TimedMute in
    // this repo). Delayed via ActionChain like LoginGreeter - chat sent immediately on enter-world is lost.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), nameof(Player.PlayerEnterWorld))]
    public static void PostEnter(Player __instance)
    {
        try
        {
            var cfg = Cfg;
            if (cfg == null || !cfg.Enabled) return;
            var p = __instance;

            new ActionChain(p, () => { }).AddDelaySeconds(cfg.DelaySeconds).AddAction(p, () =>
            {
                try
                {
                    var node = MyNode(p);
                    if (node == null) return;
                    string? motd;
                    lock (gate) motds.TryGetValue(MonarchGuid(node), out motd);
                    if (!string.IsNullOrEmpty(motd) && p.Session != null)
                        Say(p.Session, $"[Allegiance MOTD] {motd}");
                }
                catch (Exception e) { ModManager.Log($"[AllegianceMotd] {e.Message}"); }
            }).EnqueueChain();
        }
        catch (Exception e) { ModManager.Log($"[AllegianceMotd] {e.Message}"); }
    }
}
