using System.Text;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Shared.Mods;

namespace ContractTracker;

// IDEAS.md idea 60: player-facing /mycontracts, the self-service equivalent of the Sentinel-only
// @contract command (which only a Sentinel/Admin can point at anyone). This is read-only, no
// Harmony patches at all - just a command handler reading the caller's own contract state.
//
// Every member below was re-verified against the live ACE source (ACEmulator/ACE, master branch),
// not inferred from bare-identifier usage:
//   Player.ContractManager - public field, ACE.Server.WorldObjects/Player.cs:
//       "public ContractManager ContractManager;"
//   ContractManager.ContractTrackerTable - public get-only property, ACE.Server.WorldObjects.Managers/
//       ContractManager.cs: "public Dictionary<uint, ContractTracker> ContractTrackerTable { get { ... } }"
//       (computed live from Player.Character.GetContractsIds() each read - no caching to go stale)
//   ContractTracker.Contract - public get-only property, ACE.Server.Network.Structure/ContractTracker.cs
//       (resolves from DatManager.PortalDat.ContractTable by ContractId; can be null if the contract's
//       DAT entry is missing - guarded below)
//   ContractTracker.Stage - public field of type ACE.Server.Network.Structure.ContractStage
//   ContractTracker.TimeWhenDone / TimeWhenRepeats - public fields, double (seconds REMAINING, not an
//       absolute timestamp - ContractTracker's own constructor sets them from
//       QuestManager.GetNextSolveTime(...).TotalSeconds, a countdown, confirmed by reading that
//       constructor's body directly)
//   Contract.ContractId / Contract.ContractName - public get (private set), ACE.DatLoader.Entity/Contract.cs
//
// ContractStage enum (ACE.Server.Network.Structure/ContractTracker.cs) - the idea's own risk note said
// "only seen partially" and was right to say so. The REAL enum has FOUR named values, not two:
//   Available = 0x1, InProgress = 0x2, DoneOrPendingRepeat = 0x3, ProgressCounter = 0x4
// AND ContractTracker's own CheckAndSetStage() does `Stage = ContractStage.ProgressCounter + progress;`
// for a progress-flag contract - meaning the runtime value can be ProgressCounter+N for any N, an enum
// value never named in the type at all. Cast-to-int + a generic fallback (below) is therefore not just
// defensive, it is required for this enum to be handled correctly at all.
//
// "mycontracts" re-confirmed free against a fresh dump of %TEMP%\cmds.txt (327 built-ins, zero hits for
// "mycontracts"); it does not collide with the built-in @contract name pattern either.
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

    // Fully qualified: our own project namespace is also "ContractTracker", which shadows the ACE
    // type of the same name from ACE.Server.Network.Structure (a namespace wins over a same-named
    // type from a `using` when both are in scope) - a bare "ContractTracker" here resolves to the
    // namespace and fails to compile (confirmed by a real compiler error, CS0118).
    private static string DescribeStage(ACE.Server.Network.Structure.ContractTracker tracker)
    {
        // ProgressCounter is a base value, not a ceiling - CheckAndSetStage adds the player's progress
        // count on top of it, so any value >= ProgressCounter is a real, expected "in progress, N steps
        // done" state, not something unrecognized.
        switch (tracker.Stage)
        {
            case ContractStage.Available:
                return "available";
            case ContractStage.InProgress:
                return tracker.TimeWhenDone > 0
                    ? $"in progress ({FormatSeconds(tracker.TimeWhenDone)} until due)"
                    : "in progress";
            case ContractStage.DoneOrPendingRepeat:
                return tracker.TimeWhenRepeats > 0
                    ? $"done - repeatable in {FormatSeconds(tracker.TimeWhenRepeats)}"
                    : "done";
            default:
                if (tracker.Stage >= ContractStage.ProgressCounter)
                {
                    var progress = (int)tracker.Stage - (int)ContractStage.ProgressCounter;
                    return $"in progress ({progress} step(s) completed)";
                }
                // Generic fallback for any stage value not accounted for above (per LOOP.md risk note -
                // never assume the enum's named values are the only ones that can appear).
                return $"unrecognized stage ({(int)tracker.Stage})";
        }
    }

    private static string FormatSeconds(double seconds)
    {
        if (seconds <= 0)
            return "now";

        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{(int)span.TotalSeconds}s";
    }

    [CommandHandler("mycontracts", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Lists your own tracked contracts, their stage, and time remaining.", "")]
    public static void HandleMyContracts(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ContractTracker is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var manager = player.ContractManager;
        if (manager == null) { Say(session, "You have no contract tracking data."); return; }

        var table = manager.ContractTrackerTable;
        if (table == null || table.Count == 0) { Say(session, "You have no tracked contracts."); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"Your contracts ({table.Count}):");

        foreach (var kvp in table)
        {
            var tracker = kvp.Value;
            var contract = tracker.Contract; // can be null if the DAT entry is missing
            var name = contract?.ContractName ?? $"(unknown contract 0x{kvp.Key:X8})";

            sb.AppendLine($"- {name}: {DescribeStage(tracker)}");
        }

        Say(session, sb.ToString().TrimEnd());
    }
}
