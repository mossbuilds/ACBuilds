using ACE.Database.Models.World;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Shared.Mods;

namespace CraftForecast;

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

    // Same own-inventory-only search as PriceCheck's FindInOwnInventoryByName (Container.Inventory /
    // Player.EquippedObjects are public, Container.cs / Creature_Equipment.cs, verified). Recurses
    // into side containers, matches on WorldObject.Name (exact, else first substring).
    private static WorldObject? FindInOwnInventoryByName(Player player, string name)
    {
        WorldObject? partial = null;

        bool Matches(WorldObject wo)
        {
            if (string.Equals(wo.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            if (partial == null && wo.Name != null && wo.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                partial = wo;
            return false;
        }

        WorldObject? Search(Container container)
        {
            foreach (var item in container.Inventory.Values)
            {
                if (Matches(item)) return item;
                if (item is Container sub)
                {
                    var found = Search(sub);
                    if (found != null) return found;
                }
            }
            return null;
        }

        var exact = Search(player);
        if (exact != null) return exact;

        foreach (var item in player.EquippedObjects.Values)
        {
            if (Matches(item)) return item;
        }

        return partial;
    }

    // Same technique as PriceCheck for the caller's last-appraised item: CommandHandlerHelper is
    // `internal static` (ACE.Server.Command.Handlers, verified) and not reachable from a mod, so we
    // inline its GetLastAppraisedObject() body: Player.RequestedAppraisalTarget (public uint?,
    // Player_Properties.cs, verified) resolved with Player.FindObject(.., SearchLocations.Everywhere, ..)
    // (public, Player_Inventory.cs, verified).
    private static WorldObject? GetLastAppraisedItem(Player player)
    {
        var targetId = player.RequestedAppraisalTarget;
        if (targetId == null) return null;

        return player.FindObject(targetId.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
    }

    [CommandHandler("forecast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0,
        "Previews a combine's success chance before you commit. Never performs the combine.",
        "<target item name>")]
    public static void HandleForecast(Session session, params string[] parameters)
    {
        var cfg = Cfg;
        if (cfg == null || !cfg.Enabled) { Say(session, "CraftForecast is not available."); return; }

        var player = session?.Player;
        if (player == null) return;

        // Source (tool/salvage) is always the caller's last-appraised item - never named on the
        // command line, per the brief.
        var source = GetLastAppraisedItem(player);
        if (source == null)
        {
            Say(session, "Appraise the tool or salvage item first (examine it), then run /forecast <target item name>.");
            return;
        }

        if (parameters.Length == 0)
        {
            Say(session, "Usage: /forecast <target item name> - name an item in your own inventory or equipped to combine the appraised item with.");
            return;
        }

        var targetName = string.Join(" ", parameters);
        // Target found by name only in the caller's OWN inventory or equipment - never another
        // player's, per the brief.
        var target = FindInOwnInventoryByName(player, targetName);
        if (target == null)
        {
            Say(session, $"You aren't carrying or wearing anything named \"{targetName}\".");
            return;
        }

        if (source == target)
        {
            Say(session, "Appraise a different item than the one you're naming as the target - a combine can't use an item on itself.");
            return;
        }

        // RecipeManager.GetRecipe/GetNewRecipe (both public static, ACE.Server.Managers, verified)
        // are pure lookups against the cached cookbook table / recipe-search table - no world state
        // is written by either.
        Recipe? recipe;
        try
        {
            recipe = RecipeManager.GetRecipe(player, source, target);
        }
        catch (Exception ex)
        {
            Say(session, $"Couldn't look up a recipe for that combination ({ex.GetType().Name}).");
            return;
        }

        if (recipe == null)
        {
            Say(session, $"No recipe found: the {source.Name} can't be used on the {target.Name}.");
            return;
        }

        // RecipeManager.GetRecipeChance (public static, verified) dispatches to GetTinkerChance for
        // tinkering recipes, otherwise runs the plain skill-check formula. Both are read: they look
        // up the player's own trained skill and, on failure paths, send the SAME "not trained" chat
        // message a real combine attempt would send (SendWeenieError / a GameMessageSystemChat) -
        // informational only, no PropertyInt/PropertyFloat is written to source, target or player,
        // and no item is created, destroyed or consumed. GetTinkerChance also reads
        // WorldObject.Workmanship, whose getter can, in the rare "pre-workmanship-fix item" case,
        // rewrite its own already-inconsistent ItemWorkmanship value into the correct range
        // (see WorldObject_Properties.cs) - that self-correction is an existing ACE getter behavior
        // that happens on ANY appraisal of such an item, not something this mod introduces or a
        // combine-specific mutation.
        double? chance;
        try
        {
            chance = RecipeManager.GetRecipeChance(player, source, target, recipe);
        }
        catch (Exception ex)
        {
            Say(session, $"Couldn't compute a chance for that combination ({ex.GetType().Name}).");
            return;
        }

        if (chance == null)
        {
            Say(session, "Couldn't compute a chance for that combination (you may not be trained in the required skill).");
            return;
        }

        var isTinkering = recipe.IsTinkering();

        var skillId = (Skill)recipe.Skill;
        var skill = player.GetCreatureSkill(skillId);

        Say(session, $"--- Forecast: {source.Name} -> {target.Name} ---");
        Say(session, $"Tool/salvage: {source.Name}");
        Say(session, $"Target: {target.Name}");
        Say(session, $"Skill: {skillId} (current {skill.Current}, {skill.AdvancementClass})");

        if (isTinkering)
        {
            Say(session, $"Tool workmanship: {source.Workmanship?.ToString("0.0") ?? "unknown"}");
            Say(session, $"Target workmanship: {target.Workmanship?.ToString("0.0") ?? "unknown"}");
            Say(session, $"Tinkers already applied to target: {target.NumTimesTinkered}");
            if (recipe.IsImbuing())
                Say(session, "This is an imbuing recipe (success chance divided by 3, per ACE's formula).");
        }
        else
        {
            Say(session, $"Recipe difficulty: {recipe.Difficulty}");
        }

        Say(session, $"Estimated success chance: {Math.Clamp(chance.Value, 0.0, 1.0) * 100:0.#}%");
        Say(session, "This is an estimate; ACE rolls the actual chance at craft time. No combine was performed, nothing was consumed or changed.");
    }
}
