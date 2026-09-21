using ACE.Shared.Mods;

namespace MilestoneRewards;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(MilestoneRewards), new PatchClass(this));
}
