using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace PetSpells;

[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;
    private static Timer? timer;
    private static Spell? TargetSpell, OwnerSpell;
    private static readonly Dictionary<uint, long> LastPetCast = new();
    private static long Casts;

    private static Spell? Load(string name) =>
        !string.IsNullOrWhiteSpace(name) && Enum.TryParse<SpellId>(name, out var id) ? new Spell(id) : null;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        TargetSpell = Load(Cfg.SpellOnTarget);
        OwnerSpell = Load(Cfg.SpellOnOwner);
        timer?.Dispose();
        timer = new Timer(_ => Sweep(), null, 1000, 1000);
        ModManager.Log("[PetSpells] ready: /petspells" + (Cfg.Enabled ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    public override void Stop() { timer?.Dispose(); timer = null; base.Stop(); }

    // Timer thread: only reads and queues. The cast itself runs on the pet's landblock via ActionChain.
    private static void Sweep()
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true }) return;
        try
        {
            var now = Environment.TickCount64;
            var every = Math.Max(5, cfg.CastIntervalSeconds) * 1000L;
            var perOwner = new Dictionary<uint, int>();
            var cap = Math.Max(1, cfg.MaxCastsPerSecondPerOwner);
            foreach (var lb in LandblockManager.GetLoadedLandblocks())
                foreach (var wo in lb.GetAllWorldObjectsForDiagnostics())
                {
                    if (wo is not CombatPet pet || pet.IsDestroyed || !pet.IsAlive || pet.Location == null) continue;
                    if (pet.PetOwner is not { } owner || owner == 0 || !new ACE.Entity.ObjectGuid(owner).IsPlayer()) continue;
                    if (!cfg.MinionWcids.Contains(pet.WeenieClassId)) continue;
                    var op = PlayerManager.GetOnlinePlayer(owner);
                    if (op == null || op.IsLoggingOut || op.Location == null || op.Location.Landblock != pet.Location.Landblock) continue;
                    if (op.Location.DistanceTo(pet.Location) > cfg.MaxRange) continue;
                    lock (LastPetCast)
                    {
                        if (LastPetCast.TryGetValue(pet.Guid.Full, out var t) && now - t < every) continue;
                        perOwner.TryGetValue(owner, out var n);
                        if (n >= cap) continue;
                        perOwner[owner] = n + 1;
                        LastPetCast[pet.Guid.Full] = now;
                    }
                    var p = pet; var o = op;
                    new ActionChain(p, () => Cast(p, o, cfg)).EnqueueChain();
                }
        }
        catch (Exception ex) { ModManager.Log("[PetSpells] sweep failed: " + ex.Message); }
    }

    private static void Cast(CombatPet pet, Player owner, Settings cfg)
    {
        if (pet.IsDestroyed || !pet.IsAlive || owner.IsLoggingOut) return;
        if (!cfg.ManaFree && cfg.ManaCost > 0)
        {
            if (pet.Mana.Current < (uint)cfg.ManaCost) return;
            pet.UpdateVitalDelta(pet.Mana, -cfg.ManaCost);
        }
        // Debuff/curse on the current target; never at players (CanDamage) or dead targets.
        if (cfg.Debuff && TargetSpell != null && pet.AttackTarget is Creature t && !t.IsDead && t is not Player
            && pet.CanDamage(t) && pet.Location.DistanceTo(t.Location) <= cfg.MaxRange)
        { pet.TryCastSpell(TargetSpell, t, tryResist: true); Casts++; }
        if (cfg.Buff && OwnerSpell != null && !owner.IsDead)
        { pet.TryCastSpell(OwnerSpell, owner, tryResist: false); Casts++; }
    }

    // "petspells" is not one of ACE's 327 built-in command names.
    [CommandHandler("petspells", AccessLevel.Admin, CommandHandlerFlag.None, 0, "Shows PetSpells status.")]
    public static void HandleStatus(Session session, params string[] parameters)
    {
        var c = Cfg;
        var msg = c == null ? "PetSpells: not loaded."
            : $"PetSpells: enabled={c.Enabled}, target={c.SpellOnTarget}, owner={c.SpellOnOwner}, every {Math.Max(5, c.CastIntervalSeconds)}s, range {c.MaxRange}, casts so far {Casts}.";
        if (session != null) session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }
}
