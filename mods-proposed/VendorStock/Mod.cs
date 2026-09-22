using ACE.Shared.Mods;

namespace VendorStock;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(VendorStock), new PatchClass(this));
}
