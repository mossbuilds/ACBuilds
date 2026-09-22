using ACE.Shared.Mods;

namespace QuestFlagInspector;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(QuestFlagInspector), new PatchClass(this));
}
