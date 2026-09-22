using ACE.Shared.Mods;

namespace QuestChestPreview;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(QuestChestPreview), new PatchClass(this));
}
