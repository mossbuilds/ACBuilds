using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace ComponentPrecheck;

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

    [CommandHandler("compcheck", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1,
        "Previews whether you have the components a spell would consume, without casting or consuming anything.",
        "<spellId>")]
    public static void HandleCompCheck(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "ComponentPrecheck is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        if (parameters.Length == 0 || !uint.TryParse(parameters[0], out var spellId))
        {
            Say(session, "Usage: /compcheck <spellId> - the numeric spell ID, e.g. the one shown by @spellinfo or a spell reference.");
            return;
        }

        // Spell(uint, bool loadDB = true) is a public constructor (Entity/Spell.cs, verified). NotFound
        // (public bool, verified) is true when the DAT-file spell table has no entry for this ID - this
        // never touches the player or any item, it only loads static spell definition data.
        var spell = new Spell(spellId);
        if (spell.NotFound)
        {
            Say(session, $"No spell found for spell ID {spellId}.");
            return;
        }

        // Server-wide "do components matter at all" switches, read the SAME way
        // Player.HasComponentsForSpell/TryBurnComponents read them (Player_Magic.cs, verified):
        // Player.SpellComponentsRequired (per-player override, public bool, Player_Properties.cs)
        // combined with the require_spell_comps server property. When either says components don't
        // matter, HasComponentsForSpell always returns true and no cast would ever burn anything.
        var compsMatter = player.SpellComponentsRequired && PropertyManager.GetBool("require_spell_comps").Item;

        Say(session, $"--- Component precheck: {spell.Name} (id {spell.Id}) ---");

        if (!compsMatter)
        {
            Say(session, "Components are not required right now (require_spell_comps is off, or you have a safe-components override) - this cast would not consume anything.");
            return;
        }

        // Player.HasComponentsForSpell(Spell) is public (Player_Magic.cs, verified) and is PURE: it only
        // reads spell.Formula.GetRequiredComps() and Container.GetNumInventoryItemsOfWCID(wcid) (both
        // public, SpellFormula.cs / Container.cs, verified) against the player's own inventory. It never
        // calls TryBurnComponents and never removes, decrements or touches any item.
        var hasAll = player.HasComponentsForSpell(spell);

        if (hasAll)
        {
            Say(session, "You have all the components this cast needs.");
            return;
        }

        // To name which components are missing, this replicates HasComponentsForSpell's own read-only
        // logic component-by-component instead of calling the mutating TryBurnComponents to find out:
        // spell.Formula.GetPlayerFormula(player) (public, SpellFormula.cs, verified) resolves the
        // player's current formula the same way HasComponentsForSpell does, then each component index
        // in Formula.CurrentFormula (public List<uint>, verified) is resolved to a display name via the
        // static SpellFormula.SpellComponentsTable.SpellComponents dictionary (SpellComponentBase.Name,
        // public string, ACE.DatLoader, verified) and to a WCID via the public static
        // Spell.GetComponentWCID(uint) - all pure lookups against cached DAT data, nothing is read from
        // or written to any item or the player's inventory beyond the same read
        // GetNumInventoryItemsOfWCID already performs.
        spell.Formula.GetPlayerFormula(player);

        var required = new Dictionary<uint, (int Needed, string Name)>();
        foreach (var component in spell.Formula.CurrentFormula)
        {
            var wcid = Spell.GetComponentWCID(component);
            var name = SpellFormula.SpellComponentsTable.SpellComponents.TryGetValue(component, out var spellComponent)
                ? spellComponent.Name
                : $"WCID {wcid}";

            if (required.TryGetValue(wcid, out var entry))
                required[wcid] = (entry.Needed + 1, entry.Name);
            else
                required[wcid] = (1, name);
        }

        Say(session, "Missing components:");
        foreach (var kvp in required)
        {
            var wcid = kvp.Key;
            var needed = kvp.Value.Needed;
            var have = player.GetNumInventoryItemsOfWCID(wcid);
            if (have >= needed) continue;

            Say(session, $"  {kvp.Value.Name}: have {have}, need {needed}");
        }

        Say(session, "This is a preview only - nothing was cast, and no component was consumed.");
    }
}
