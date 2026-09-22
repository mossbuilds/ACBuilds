using ACE.Shared.Mods;

namespace AllegianceMotd;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AllegianceMotd), new PatchClass(this));
}
