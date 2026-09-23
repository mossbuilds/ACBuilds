using ACE.Shared.Mods;

namespace AllegianceXpLedger;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AllegianceXpLedger), new PatchClass(this));
}
