using ACE.Database;
using ACE.Entity;
using ACE.Server.Command;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
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

    // ---- /boost: EXPERIMENT. Server pushes extra forward velocity to the player's client while running (the client caps its own run speed). ----
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, float> Boosted = new();
    private static System.Threading.Timer? boostTimer;

    [CommandHandler("boost", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "EXPERIMENT: push extra forward speed to yourself while running. /boost [extra units per second, default 18], /boost off", "[extra|off]")]
    public static void HandleBoost(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;
        var guid = player.Guid.Full;
        if (parameters.Length > 0 && parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            Boosted.TryRemove(guid, out _);
            session.Network.EnqueueSend(new GameMessageSystemChat("Boost off.", ChatMessageType.Broadcast));
            return;
        }
        var extra = 18f;
        if (parameters.Length > 0 && !float.TryParse(parameters[0], out extra))
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /boost [extra 1-50] | /boost off", ChatMessageType.Broadcast));
            return;
        }
        extra = Math.Clamp(extra, 1f, 50f);   // ACE/AC clamp velocity to 50 (PhysicsGlobals.MaxVelocity)
        Boosted[guid] = extra;
        boostTimer ??= new System.Threading.Timer(_ => BoostTick(), null, 100, 100);
        session.Network.EnqueueSend(new GameMessageSystemChat($"Boost on: +{extra} while running forward. /boost off to stop.", ChatMessageType.Broadcast));
        ModManager.Log($"{Tag} boost on for {player.Name} +{extra}");
    }

    private static void BoostTick()
    {
        foreach (var kv in Boosted)
        {
            var player = PlayerManager.GetOnlinePlayer(kv.Key);
            if (player == null) { Boosted.TryRemove(kv.Key, out _); continue; }
            var extra = kv.Value;
            new ActionChain(player, () =>
            {
                try
                {
                    var mi = player.PhysicsObj?.MovementManager?.MotionInterpreter;
                    if (mi == null || mi.InterpretedState.ForwardCommand != (uint)MotionCommand.RunForward) return;
                    player.PhysicsObj.set_local_velocity(new System.Numerics.Vector3(0, extra, 0), false);
                    player.EnqueueBroadcast(new GameMessageVectorUpdate(player));
                }
                catch (Exception ex) { ModManager.Log($"{Tag} boost tick: {ex.Message}", ModManager.LogLevel.Warn); }
            }).EnqueueChain();
        }
    }

    // ---- /warp: while running forward, hop the player forward in short steps, stopping at cliffs / steep or unwalkable ground / indoors. ----
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, float> Warping = new();
    private static System.Threading.Timer? warpTimer;

    [CommandHandler("warp", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Fast travel outdoors: while you run forward the server hops you ahead in short steps. /warp [step 5-60, default 25], /warp off", "[step|off]")]
    public static void HandleWarp(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;
        var guid = player.Guid.Full;
        if (parameters.Length > 0 && parameters[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            Warping.TryRemove(guid, out _);
            session.Network.EnqueueSend(new GameMessageSystemChat("Warp off.", ChatMessageType.Broadcast));
            return;
        }
        var step = 25f;
        if (parameters.Length > 0 && !float.TryParse(parameters[0], out step))
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /warp [step 5-60] | /warp off", ChatMessageType.Broadcast));
            return;
        }
        step = Math.Clamp(step, 5f, 60f);
        Warping[guid] = step;
        warpTimer ??= new System.Threading.Timer(_ => WarpTick(), null, 150, 150);
        session.Network.EnqueueSend(new GameMessageSystemChat($"Warp on: {step} units per hop while running forward, outdoors only, stops at cliffs. /warp off to stop.", ChatMessageType.Broadcast));
        ModManager.Log($"{Tag} warp on for {player.Name} step {step}");
    }

    private static void WarpTick()
    {
        foreach (var kv in Warping)
        {
            var player = PlayerManager.GetOnlinePlayer(kv.Key);
            if (player == null) { Warping.TryRemove(kv.Key, out _); continue; }
            var step = kv.Value;
            new ActionChain(player, () =>
            {
                try
                {
                    if (player.Teleporting || player.Location.Indoors) return;
                    var mi = player.PhysicsObj?.MovementManager?.MotionInterpreter;
                    if (mi == null || mi.InterpretedState.ForwardCommand != (uint)MotionCommand.RunForward) return;

                    // walk the path in 4-unit samples; stop before the first bad one
                    var prevZ = player.Location.PositionZ;
                    Position? good = null;
                    for (var d = 4f; d <= step + 0.01f; d += 4f)
                    {
                        var p = player.Location.InFrontOf(d);
                        p.LandblockId = new LandblockId(p.GetCell());
                        if (p.Indoors) break;
                        var z = p.GetTerrainZ();
                        if (!p.IsWalkable() || Math.Abs(z - prevZ) > 3f) break;
                        p.PositionZ = z + 0.5f;
                        prevZ = z;
                        good = p;
                    }
                    if (good != null) player.Teleport(good);
                }
                catch (Exception ex) { ModManager.Log($"{Tag} warp tick: {ex.Message}", ModManager.LogLevel.Warn); }
            }).EnqueueChain();
        }
    }

    // ---- /leap: Hulk jump. Launches the player along a ballistic arc; fall damage is suppressed for the flight. ----
    [CommandHandler("leap", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0, "Hulk leap: launch yourself in an arc in the direction you face (be standing still on the ground). /leap [power 0.2-1.0, default 1] - range about 250 units at 1.0", "[power]")]
    public static void HandleLeap(Session session, params string[] parameters)
    {
        var player = session?.Player;
        if (player == null) return;
        var power = 1f;
        if (parameters.Length > 0 && !float.TryParse(parameters[0], out power))
        {
            session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /leap [power 0.2-1.0]", ChatMessageType.Broadcast));
            return;
        }
        power = Math.Clamp(power, 0.2f, 1f);
        var v = 35f * power;                    // 45 degrees, speed ~49.5 < the 50 cap; range about v*v/gravity*... ~250 at 1.0
        var wasInvincible = player.Invincible;
        new ActionChain(player, () =>
        {
            var po = player.PhysicsObj;
            if (po == null) return;
            player.Invincible = true;           // the landing would otherwise do ~150 fall damage
            po.TransientState &= ~(TransientStateFlags.Contact | TransientStateFlags.WaterContact);
            po.calc_acceleration();
            po.set_on_walkable(false);
            po.set_local_velocity(new System.Numerics.Vector3(0, v, v), false);
            po.MovementManager?.MotionInterpreter?.PendingMotions.Clear();
            po.IsAnimating = false;
            var movementData = new MovementData(player) { IsAutonomous = true, MovementType = MovementType.Invalid };
            movementData.Invalid = new MovementInvalid(movementData);
            player.EnqueueBroadcast(new GameMessageUpdateMotion(player, movementData));
            player.EnqueueBroadcast(new GameMessageVectorUpdate(player));
            ModManager.Log($"{Tag} leap {player.Name} v={v}");
        }).EnqueueChain();
        var restore = new ActionChain(player, () => { });
        restore.AddDelaySeconds(15);
        restore.AddAction(player, () => { player.Invincible = wasInvincible; });
        restore.EnqueueChain();
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
