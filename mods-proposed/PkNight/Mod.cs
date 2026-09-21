using ACE.Shared.Mods;

namespace PkNight;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PkNight), new PatchClass(this));
}
