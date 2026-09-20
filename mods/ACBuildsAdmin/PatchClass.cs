using ACE.Database;
using ACE.Entity;
using ACE.Server.Command;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;
using Position = ACE.Entity.Position;

namespace ACBuildsAdmin;

/// <summary>
/// Console-only commands (flag ConsoleInvoke: a player can NOT run these in game; only someone with access to the server console can).
/// Run them from the host, output appears in `docker logs`:
///   docker exec ace-vr-server sh -c "echo 'spawnnear 900021224 Mmm' > /ace/console.in"
///
///   whereis [player name]                 live position of one online player, or of everyone online
///   /dash [distance]                      IN GAME, Admin: teleport forward on the ground (run speed is capped by the client)
///   spawnnear (wcid or classname) (player name)   create the object next to that online player (temporary, like /create)
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private const string Tag = "[ACBuildsAdmin]";
    private const double MinItemDistance = 2;

    public override Task OnWorldOpen()
    {
        Settings = SettingsContainer.Settings;
        ModManager.Log($"{Tag} ready: whereis, spawnnear (console only), /dash (admin, in game)");
        return base.OnWorldOpen();
    }

    /// <summary>Admin-only in-game hop: teleports the caller forward along the ground. The client caps run speed (skill 800), so this is the way to travel faster.</summary>
    [CommandHandler("dash", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Teleport yourself forward in the direction you face (outdoors only). Default 100, max 2000.", "[distance]")]
    public static void HandleDash(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;
        var dist = 100f;
        if (parameters.Length > 0 && (!float.TryParse(parameters[0], out dist) || dist < 2))
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /dash [distance 2-2000]", ChatMessageType.Broadcast));
            return;
        }
        dist = Math.Min(dist, 2000f);
        if (player.Location.Indoors)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("/dash works outdoors only.", ChatMessageType.Broadcast));
            return;
        }
        var dest = player.Location.InFrontOf(dist);
        dest.LandblockId = new LandblockId(dest.GetCell());
        if (dest.Indoors)
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Something is in the way (that lands indoors); try a shorter dash.", ChatMessageType.Broadcast));
            return;
        }
        dest.PositionZ = dest.GetTerrainZ() + 0.5f;
        player.Teleport(dest);
    }

    private static string Describe(Player p)
    {
        var pos = p.Location;
        return $"{p.Name}: cell=0x{pos.Cell:X8} x={pos.PositionX:F1} y={pos.PositionY:F1} z={pos.PositionZ:F1} | {pos.ToLOCString()}";
    }

    [CommandHandler("whereis", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, 0,
        "Shows where online players are (live position).", "[player name]  - omit the name to list everyone online")]
    public static void HandleWhereIs(Session session, params string[] parameters)
    {
        var name = string.Join(" ", parameters ?? []).Trim();
        if (name.Length == 0)
        {
            var all = PlayerManager.GetAllOnline();
            ModManager.Log($"{Tag} {all.Count} player(s) online");
            foreach (var p in all)
                ModManager.Log($"{Tag} {Describe(p)}");
            return;
        }
        var player = PlayerManager.GetOnlinePlayer(name);
        ModManager.Log(player == null ? $"{Tag} '{name}' is not online (or not in the world yet)" : $"{Tag} {Describe(player)}");
    }

    [CommandHandler("spawnnear", AccessLevel.Admin, CommandHandlerFlag.ConsoleInvoke, 2,
        "Creates an object next to an online player (temporary, exactly like /create).", "<wcid or classname> <player name>")]
    public static void HandleSpawnNear(Session session, params string[] parameters)
    {
        if (parameters == null || parameters.Length < 2)
        {
            ModManager.Log($"{Tag} usage: spawnnear <wcid or classname> <player name>");
            return;
        }
        var what = parameters[0];
        var name = string.Join(" ", parameters.Skip(1)).Trim();

        var player = PlayerManager.GetOnlinePlayer(name);
        if (player == null)
        {
            ModManager.Log($"{Tag} spawnnear: '{name}' is not online (or not in the world yet)");
            return;
        }

        var weenie = uint.TryParse(what, out var wcid)
            ? DatabaseManager.World.GetCachedWeenie(wcid)
            : DatabaseManager.World.GetCachedWeenie(what);
        if (weenie == null)
        {
            ModManager.Log($"{Tag} spawnnear: '{what}' is not a valid weenie (was it applied, and the cache cleared?)");
            return;
        }
        if (weenie.WeenieType is WeenieType.Admin or WeenieType.Sentinel or WeenieType.Undef)
        {
            ModManager.Log($"{Tag} spawnnear: refusing to spawn a {weenie.WeenieType}");
            return;
        }

        // Object creation must happen on the player's own landblock thread, not on the console thread.
        new ActionChain(player, () =>
        {
            var obj = WorldObjectFactory.CreateNewWorldObject(weenie);
            if (obj == null)
            {
                ModManager.Log($"{Tag} spawnnear: could not create {what}");
                return;
            }
            if (obj.WeenieType == WeenieType.Creature)
                obj.Location = player.Location.InFrontOf(5f, true);       // same placement as ACE's /create for creatures
            else
                obj.Location = player.Location.InFrontOf(Math.Max(MinItemDistance, obj.UseRadius ?? MinItemDistance));
            obj.Location.LandblockId = new LandblockId(obj.Location.GetCell());

            if (obj.EnterWorld())
                ModManager.Log($"{Tag} spawned {obj.Name} (wcid {weenie.WeenieClassId}, 0x{obj.Guid.Full:X8}) next to {player.Name} at {obj.Location.ToLOCString()}");
            else
                ModManager.Log($"{Tag} spawnnear: {obj.Name} could not enter the world (blocked position?)");
        }).EnqueueChain();
        ModManager.Log($"{Tag} spawnnear: queued {what} next to {player.Name}");
    }
}
