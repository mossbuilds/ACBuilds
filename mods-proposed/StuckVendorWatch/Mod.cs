using ACE.Shared.Mods;

namespace StuckVendorWatch;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(StuckVendorWatch), new PatchClass(this));
}
