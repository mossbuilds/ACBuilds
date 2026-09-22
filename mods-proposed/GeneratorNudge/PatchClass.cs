using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace GeneratorNudge;

// IDEAS.md idea 70, Round 11's last idea. Closes the exact gap Round 9's GeneratorAudit (idea 57)
// flagged and declined to act on: "no public ResetGenerator-style method was found by name search;
// only the read-only diagnostic half of the idea (GeneratorAudit) survived."
//
// Found this round by reading the FULL file (not name-searching): direct fetch of
// Source/ACE.Server/WorldObjects/WorldObject_Generators.cs from ACEmulator/ACE (master), confirmed
// via github-second-brain, exact text:
//
//     public virtual void ResetGenerator()
//     {
//         foreach (var generator in GeneratorProfiles)
//         {
//             generator.Reset();
//         }
//     }
//
// This is a plain public virtual instance method on WorldObject itself - no internal/protected
// access, no special caller context, callable directly on any generator reference a mod can obtain
// the normal way (Player.FindObject, same last-appraised lookup VendorStock/PriceCheck already use).
//
// WHAT IT ACTUALLY MUTATES (read in full, Source/ACE.Server/Entity/GeneratorProfile.cs,
// GeneratorProfile.Reset()):
//
//     public void Reset()
//     {
//         foreach (var rNode in Spawned.Values)
//         {
//             var wo = rNode.TryGetWorldObject();
//             if (wo != null)
//             {
//                 if (wo.IsGenerator) wo.ResetGenerator();      // recurses into nested generators
//                 if (wo.Container == Generator) { var container = Generator as Container;
//                     container?.TryRemoveFromInventory(wo.Guid); }
//                 wo.Destroy();                                  // <-- destroys every currently
//             }                                                   //     spawned object, live or not
//         }
//         CleanupProfile();   // clears Spawned + SpawnQueue, resets NextAvailable to now,
//                              // clears GeneratedTreasureItem
//     }
//
// SAFETY ANALYSIS (this is the important part):
//   - ResetGenerator() is not a special/unsafe entry point - it is the exact same call ACE's own
//     landblock unload/reload path and Chest.Reset() already exercise in normal operation
//     (confirmed by the idea's own research note and matched here against the source). It does not
//     touch threading, locking, or persistence state beyond what Destroy()/TryRemoveFromInventory
//     already handle safely on their own - both are ordinary public WorldObject/Container APIs used
//     all over ACE, not something reserved for an internal-only calling context.
//   - It IS destructive in a way staff must understand before running it: every object the
//     generator currently has spawned - INCLUDING LIVE, UNKILLED CREATURES a player may be mid-fight
//     with, and any items already sitting in the world or a vendor's shop - is unconditionally
//     Destroy()'d, not killed for loot/XP and not despawned gracefully. A nested generator (a chest
//     spawned by a generator that itself spawns loot) is reset recursively too, wiping its contents
//     as well. This is silent to the affected player: no death message, no loot, the object just
//     disappears.
//   - It is therefore SAFE to call standalone from a command in the sense that it will not corrupt
//     server state or crash anything - but it is NOT safe to invoke lightly on a generator that
//     still has live spawns staff cares about. This mod does not attempt to soften that: it calls
//     the real method with no partial/simulated variant, states the mutation plainly before acting,
//     restricts the command to Admin (a state-mutating admin action per this repo's own
//     access-level rule), and prints the exact before/after counts so staff can immediately see
//     whether anything was actually wiped, matching GeneratorAudit's already-verified
//     AllProfilesUnavailable/CurrentCreate (WorldObject_Generators.cs) for the same before/after
//     read GeneratorAudit uses to flag a stall in the first place.
//   - A generator legitimately idle/empty by design (an event trigger, a one-shot boss room, a
//     quest-gated spawn) is cheap to nudge - Reset() on an already-empty Spawned dictionary is a
//     no-op loop plus CleanupProfile(), so nudging a false positive from GeneratorAudit wastes
//     nothing. The real risk is nudging a generator GeneratorAudit correctly flagged as stalled but
//     that also happens to have live spawns still up from before the stall - so the warning is
//     phrased to cover both cases, not just the audit false-positive one.
//
// Reuses the same last-appraised-target lookup already verified by PriceCheck/VendorStock
// (Player.RequestedAppraisalTarget public uint?, Player.FindObject public) - CommandHandlerHelper's
// equivalent is internal to ACE.Server and not reachable from a mod.
//
// cmds.txt in %TEMP% checked (327 built-ins): "gennudge" is free.
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

    [CommandHandler("gennudge", AccessLevel.Admin, CommandHandlerFlag.RequiresWorld, 0,
        "Force-resets your last-appraised generator via WorldObject.ResetGenerator(). DESTROYS every object it currently has spawned, including live creatures and items - not a graceful despawn.",
        "")]
    public static void HandleGenNudge(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "GeneratorNudge is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) { Say(session, "Appraise a generator first (examine it), then use /gennudge."); return; }

        var obj = player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (obj == null) { Say(session, "Couldn't find your last appraised object - it may no longer exist."); return; }

        if (!obj.IsGenerator)
        {
            Say(session, "Your last appraised object is not a generator.");
            return;
        }

        var profileCount = obj.GeneratorProfiles?.Count ?? 0;
        var beforeUnavailable = obj.AllProfilesUnavailable;
        var beforeCreate = obj.CurrentCreate;

        Say(session,
            $"GeneratorNudge: 0x{obj.Guid.Full:X8} {obj.Name} - before: AllProfilesUnavailable={beforeUnavailable}, " +
            $"CurrentCreate={beforeCreate} across {profileCount} profile(s). Resetting now - this DESTROYS every " +
            "object this generator currently has spawned, including live creatures and items still in the world " +
            "(not a kill, not a graceful despawn - just gone, no loot/XP). A generator idle by design (an event " +
            "trigger, a one-shot boss room, a quest-gated spawn) is harmless to nudge if truly empty, but verify " +
            "with /genaudit or by eye before nudging one you haven't checked.");

        obj.ResetGenerator();

        Say(session,
            $"GeneratorNudge: 0x{obj.Guid.Full:X8} {obj.Name} - after: AllProfilesUnavailable={obj.AllProfilesUnavailable}, " +
            $"CurrentCreate={obj.CurrentCreate}. {(beforeCreate > 0 ? $"Destroyed up to {beforeCreate} previously-spawned object(s)." : "Nothing was spawned, so nothing was destroyed - only queues/timers were reset.")}");
    }
}
