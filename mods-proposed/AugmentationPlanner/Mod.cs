using ACE.Shared.Mods;

namespace AugmentationPlanner;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AugmentationPlanner), new PatchClass(this));
}
