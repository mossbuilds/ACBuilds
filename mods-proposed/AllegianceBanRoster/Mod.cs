using ACE.Shared.Mods;

namespace AllegianceBanRoster;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AllegianceBanRoster), new PatchClass(this));
}
