using ACE.Shared.Mods;

namespace AllegianceOfficers;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AllegianceOfficers), new PatchClass(this));
}
