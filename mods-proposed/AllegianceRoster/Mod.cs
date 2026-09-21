using ACE.Shared.Mods;

namespace AllegianceRoster;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AllegianceRoster), new PatchClass(this));
}
