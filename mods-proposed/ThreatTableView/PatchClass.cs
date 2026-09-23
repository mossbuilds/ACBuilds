using System.Linq;
using ACE.Server.Entity;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ThreatTableView;

// IDEAS.md idea 86, Round 15. Sentinel-only, read-only view of a targeted/last-appraised monster's
// live combat-threat state - for diagnosing "who actually has kill credit / why did this mob switch
// targets" disputes BEFORE the kill, distinct from the already-shipped LootWatch (idea 45), which
// only audits AFTER a corpse is opened.
//
// Every member below re-verified this round by direct full-file fetch from ACEmulator/ACE (master)
// via github-second-brain:
//
//   Creature.DamageHistory  -> public DamageHistory DamageHistory { get; private set; }
//                              (Source/ACE.Server/WorldObjects/Creature_Combat.cs)
//   DamageHistory.Damagers  -> public List<DamageHistoryInfo> Damagers => TotalDamage.Values.ToList();
//   DamageHistory.TopDamager, LastDamager -> both public DamageHistoryInfo (get-only)
//   DamageHistory.TotalHealth -> public float TotalHealth
//                              (all four in Source/ACE.Server/Entity/DamageHistory.cs)
//   DamageHistoryInfo.Guid   -> public readonly ObjectGuid Guid
//   DamageHistoryInfo.Name   -> public readonly string Name
//   DamageHistoryInfo.TotalDamage -> public float TotalDamage
//   DamageHistoryInfo.IsPlayer -> public bool IsPlayer => Guid.IsPlayer();
//                              (Source/ACE.Server/Entity/DamageHistoryInfo.cs)
//   Creature.TargetingTactic / CurrentTargetingTactic -> both public
//                              (Source/ACE.Server/WorldObjects/Monster_Awareness.cs)
//   Creature.VisualAwarenessRangeSq / AuralAwarenessRangeSq -> both public float get-only
//                              (Source/ACE.Server/WorldObjects/Monster_Awareness.cs), scaled live
//                              by the "mob_awareness_range" server property (PropertyManager)
//   Creature.AttackTarget    -> the idea pointer says "used pervasively as a public member
//                              throughout Monster_Awareness.cs", but that file only ever *uses* the
//                              field, it never declares it. Full-file search this round located the
//                              actual declaration in Source/ACE.Server/WorldObjects/Monster_Combat.cs:
//                                  public WorldObject AttackTarget;
//                              confirmed public (not protected) by the additional evidence that
//                              Creature_Combat.cs's own AlertMonster() sets `monster.AttackTarget`
//                              on an unrelated Creature-typed reference, and Player.cs (a subclass)
//                              sets `creature.AttackTarget` through a plain Creature-typed variable -
//                              C#'s protected-access rule would reject that second call if the field
//                              were only protected, so it must be public (or internal; either way
//                              readable from this mod, same assembly).
//
// DamageHistoryInfo.Attacker is a WeakReference<WorldObject> - a long-dead attacker's Name/Guid
// still print fine since those are captured as plain fields at construction time, but
// TryGetAttacker() could return null if garbage collected. This command never calls TryGetAttacker()
// or TryGetPetOwner(); it only reads the plain Name/Guid/TotalDamage/IsPlayer fields.
//
// Command name checked against %TEMP%\cmds.txt (327 built-ins) - "threattable" is free.
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

    [CommandHandler("threattable", AccessLevel.Sentinel, CommandHandlerFlag.RequiresWorld, 0,
        "Shows the live combat-threat state of your last-appraised/targeted creature: current attack target, targeting tactic, awareness range, and the ranked damager list.",
        "")]
    public static void HandleThreatTable(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ThreatTableView is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Same last-appraised-target lookup already verified by PriceCheck/VendorStock/TinkerHistory/
        // GeneratorNudge: Player.RequestedAppraisalTarget (public uint?, Player_Properties.cs) resolved
        // via Player.FindObject(.., SearchLocations.Everywhere, ..) (public). CommandHandlerHelper's
        // equivalent is internal to ACE.Server and not reachable from a mod.
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise (examine) a creature first, then use /threattable."); return; }

        var obj = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (obj == null) { Say(session, "Couldn't find your last appraised object - it may no longer exist."); return; }

        if (obj is not Creature creature)
        {
            Say(session, "Your last appraised object is not a creature.");
            return;
        }

        var dh = creature.DamageHistory;

        var lines = new List<string>
        {
            $"ThreatTableView: 0x{creature.Guid.Full:X8} {creature.Name}",
            $"  AttackTarget: {(creature.AttackTarget != null ? $"{creature.AttackTarget.Name} (0x{creature.AttackTarget.Guid.Full:X8})" : "none")}",
            $"  TargetingTactic (configured): {creature.TargetingTactic} | CurrentTargetingTactic: {creature.CurrentTargetingTactic}",
            $"  VisualAwarenessRange: {System.Math.Sqrt(creature.VisualAwarenessRangeSq):F1} | AuralAwarenessRange: {System.Math.Sqrt(creature.AuralAwarenessRangeSq):F1}",
            $"  TotalHealth (damage tracked): {dh.TotalHealth:F0}"
        };

        var damagers = dh.Damagers.OrderByDescending(d => d.TotalDamage).Take(cfg.MaxDamagers).ToList();
        if (damagers.Count == 0)
        {
            lines.Add("  Damagers: none recorded.");
        }
        else
        {
            lines.Add($"  Damagers (top {damagers.Count}):");
            foreach (var d in damagers)
                lines.Add($"    {d.Name} (0x{d.Guid.Full:X8}){(d.IsPlayer ? " [player]" : "")} - {d.TotalDamage:F0} total damage");

            var top = dh.TopDamager;
            var last = dh.LastDamager;
            lines.Add($"  TopDamager: {(top != null ? $"{top.Name} (0x{top.Guid.Full:X8})" : "none")} | LastDamager: {(last != null ? $"{last.Name} (0x{last.Guid.Full:X8})" : "none")}");
        }

        foreach (var line in lines)
            Say(session, line);
    }
}
