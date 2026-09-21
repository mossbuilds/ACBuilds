using ACE.Shared.Mods;

namespace TradeLedger;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TradeLedger), new PatchClass(this));
}
