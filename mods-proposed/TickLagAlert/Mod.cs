using ACE.Shared.Mods;

namespace TickLagAlert;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TickLagAlert), new PatchClass(this));
}
