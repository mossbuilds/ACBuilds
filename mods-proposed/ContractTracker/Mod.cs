using ACE.Shared.Mods;

namespace ContractTracker;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ContractTracker), new PatchClass(this));
}
