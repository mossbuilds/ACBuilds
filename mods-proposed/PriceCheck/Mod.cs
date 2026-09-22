using ACE.Shared.Mods;

namespace PriceCheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PriceCheck), new PatchClass(this));
}
