using ACE.Shared.Mods;

namespace MinionOrders;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(MinionOrders), new PatchClass(this));
}
