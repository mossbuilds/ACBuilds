using ACE.Shared.Mods;

namespace NetWorthCheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(NetWorthCheck), new PatchClass(this));
}
