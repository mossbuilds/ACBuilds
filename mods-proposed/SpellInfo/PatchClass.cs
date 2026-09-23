using System.Linq;
using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.Database.Models.World;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared.Mods;

namespace SpellInfo;

/// <summary>
/// Read-only admin lookup against the CLIENT DAT spell table (not the ace_world.spell table, which only
/// carries a handful of server-side mechanics fields - name/school/mana/formula live in the DAT). Verified
/// against ACE master:
///   - DatManager.PortalDat.SpellTable.Spells: Dictionary&lt;uint, SpellBase&gt;
///     (Source/ACE.DatLoader/FileTypes/SpellTable.cs line 14)
///   - SpellBase fields Name/Desc/School/Category/Power/BaseMana/MetaSpellType/NonComponentTargetType/Formula
///     (Source/ACE.DatLoader/Entity/SpellBase.cs lines 9-40)
///   - DatManager.PortalDat.SpellComponentsTable.SpellComponents: Dictionary&lt;uint, SpellComponentBase&gt;,
///     each component's display Name (Source/ACE.DatLoader/FileTypes/SpellComponentsTable.cs line 24;
///     Source/ACE.DatLoader/Entity/SpellComponentBase.cs lines 7-14)
///   - "how many places reference it" uses ACE.Database.Models.World.WeeniePropertiesSpellBook.Spell (int),
///     queried through a plain WorldDbContext (Source/ACE.Database/Models/World/WeeniePropertiesSpellBook.cs
///     lines 9-30; WorldDbContext.WeeniePropertiesSpellBook DbSet, Source/ACE.Database/Models/World/WorldDbContext.cs
///     line 121) - a straight count(*) on an indexed-by-nothing table but small enough (a few thousand rows)
///     to be cheap for an admin-only, on-demand command.
/// </summary>
[HarmonyPatch]
public class PatchClass(BasicMod mod, string settingsName = "Settings.json") : BasicPatch<Settings>(mod, settingsName)
{
    private static Settings? Cfg;

    public override Task OnWorldOpen()
    {
        Cfg = SettingsContainer.Settings;
        ModManager.Log("[SpellInfo] ready: /spellinfo <id> | /spellinfo find <text>" + (Cfg is { Enabled: true } ? "" : " (disabled in Settings.json)"));
        return base.OnWorldOpen();
    }

    private static void Say(Session? s, string msg)
    {
        if (s != null) s.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
        else Console.WriteLine(msg);
    }

    [CommandHandler("spellinfo", AccessLevel.Admin, CommandHandlerFlag.None, 1,
        "Show a spell's DAT data and a player-castable guess, or find spells by name.", "<id> | find <text> | self [text]")]
    public static void HandleSpellInfo(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg is not { Enabled: true }) { Say(session, "SpellInfo is not enabled."); return; }

        if (parameters[0].Equals("self", StringComparison.OrdinalIgnoreCase))
        {
            // self-cast spells only: the client casts these on you with nothing selected - the natural fit for a
            // trigger spell whose mod ability finds its own target (nearest corpse, your minions)
            var text = parameters.Length > 1 ? string.Join(" ", parameters.Skip(1)) : "";
            Find(session, text, cfg.FindLimit, selfOnly: true);
            return;
        }

        if (parameters[0].Equals("find", StringComparison.OrdinalIgnoreCase))
        {
            if (parameters.Length < 2) { Say(session, "Usage: /spellinfo find <text>"); return; }
            var text = string.Join(" ", parameters.Skip(1));
            Find(session, text, cfg.FindLimit);
            return;
        }

        if (!uint.TryParse(parameters[0], out var id)) { Say(session, "Usage: /spellinfo <id> | /spellinfo find <text>"); return; }
        Show(session, id);
    }

    // SpellFlags.SelfTargeted = 0x8 (Source/ACE.Entity/Enum/SpellFlags.cs line 11); SpellBase.Bitfield is the spell's flag
    // word (Source/ACE.DatLoader/Entity/SpellBase.cs line 16). ACE's own Spell.IsSelfTargeted reads the same bit.
    private static bool IsSelf(SpellBase sb) => (sb.Bitfield & (uint)SpellFlags.SelfTargeted) != 0;

    private static void Find(Session session, string text, int limit, bool selfOnly = false)
    {
        var table = DatManager.PortalDat.SpellTable;
        var hits = table.Spells
            .Where(kv => kv.Value.Name != null && kv.Value.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Where(kv => !selfOnly || (IsSelf(kv.Value) && kv.Value.Formula != null && kv.Value.Formula.Count > 0))
            .OrderBy(kv => kv.Key)
            .Take(Math.Max(1, limit))
            .ToList();

        if (hits.Count == 0) { Say(session, $"No spell name contains \"{text}\"."); return; }
        Say(session, $"Spells matching \"{text}\" (up to {limit}):");
        foreach (var kv in hits)
            Say(session, $"  {kv.Key}: {kv.Value.Name}  [{(IsSelf(kv.Value) ? "self" : "target")}, power {kv.Value.Power}, {kv.Value.School}]");
    }

    private static void Show(Session session, uint id)
    {
        if (!DatManager.PortalDat.SpellTable.Spells.TryGetValue(id, out var sb) || sb == null)
        {
            Say(session, $"Spell {id}: not found in the client DAT spell table.");
            return;
        }

        Say(session, $"Spell {id}: {sb.Name}");
        Say(session, $"  School: {sb.School}   Category: {sb.Category}   MetaSpellType: {sb.MetaSpellType}");
        Say(session, $"  Power: {sb.Power}   BaseMana: {sb.BaseMana}   NonComponentTargetType: {sb.NonComponentTargetType}");
        Say(session, IsSelf(sb) ? "  Targeting: self-cast - no target needed (the client casts it on you)"
                                : "  Targeting: needs a target selected before casting");

        var comps = sb.Formula;
        string formulaText;
        if (comps == null || comps.Count == 0)
        {
            formulaText = "(none)";
        }
        else
        {
            var compTable = DatManager.PortalDat.SpellComponentsTable;
            formulaText = string.Join(", ", comps.Select(c =>
                compTable.SpellComponents.TryGetValue(c, out var cb) ? $"{c}:{cb.Name}" : c.ToString()));
        }
        Say(session, $"  Formula ({(comps?.Count ?? 0)} components): {formulaText}");

        // Heuristic only - ACE has no explicit "player castable" flag. A spell with no formula and no
        // components (typically NonComponentTargetType != None, e.g. Creature) is almost always cast
        // directly by monster/NPC code (proc, innate attack) rather than learned into a player spellbook.
        bool noFormula = comps == null || comps.Count == 0;
        bool likelyNpcOnly = noFormula && sb.NonComponentTargetType != 0;
        string castable = likelyNpcOnly ? "no (looks NPC/monster-only: no formula, NonComponentTargetType != None)"
            : noFormula ? "unknown (no formula, but NonComponentTargetType is None - could be untargeted/self-only)"
            : "yes (has a component formula, the usual sign of a learnable player spell)";
        Say(session, $"  player-castable: {castable}");

        int refCount;
        try
        {
            using var ctx = new WorldDbContext();
            refCount = ctx.WeeniePropertiesSpellBook.Count(r => r.Spell == (int)id);
            Say(session, $"  referenced in {refCount} weenie spellbook row(s) in ace_world.");
        }
        catch (Exception e)
        {
            Say(session, $"  weenie spellbook reference count unavailable: {e.Message}");
        }
    }
}
