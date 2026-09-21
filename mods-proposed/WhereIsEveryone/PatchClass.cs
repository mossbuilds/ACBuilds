using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace WhereIsEveryone;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static void Say(Session session, string msg)
    {
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    // PlayerManager.GetAllOnline() and GetOnlinePlayer(string) verified in PlayerManager.cs.
    [CommandHandler("who2", AccessLevel.Admin, CommandHandlerFlag.None, 0, "List online players with level and location.", "")]
    public static void HandleWho(Session session, params string[] parameters)
    {
        var all = PlayerManager.GetAllOnline();
        Say(session, $"Online players: {all.Count}");
        foreach (var p in all)
            Say(session, $"{p.Name} L{p.Level} lb {p.Location?.Landblock:X4} {p.Location?.ToLOCString()}");
    }

    // Player.Teleport(Position, bool) verified in Player_Location.cs.
    [CommandHandler("gotoplayer", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, "Teleport yourself to an online player.", "<name>")]
    public static void HandleGoto(Session session, params string[] parameters)
    {
        var target = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters));
        if (target == null) { Say(session, "Player not online."); return; }
        session.Player.Teleport(new ACE.Entity.Position(target.Location));
    }

    [CommandHandler("bringplayer", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 1, "Teleport an online player to you.", "<name>")]
    public static void HandleBring(Session session, params string[] parameters)
    {
        var target = PlayerManager.GetOnlinePlayer(string.Join(" ", parameters));
        if (target == null) { Say(session, "Player not online."); return; }
        target.Teleport(new ACE.Entity.Position(session.Player.Location));
    }
}
