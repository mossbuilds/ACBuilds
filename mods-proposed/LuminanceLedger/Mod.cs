using ACE.Shared.Mods;

namespace LuminanceLedger;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(LuminanceLedger), new PatchClass(this));
}
