using ACE.Shared.Mods;

namespace StuckRescue;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(StuckRescue), new PatchClass(this));
}
