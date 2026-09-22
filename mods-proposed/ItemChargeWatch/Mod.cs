using ACE.Shared.Mods;

namespace ItemChargeWatch;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ItemChargeWatch), new PatchClass(this));
}
