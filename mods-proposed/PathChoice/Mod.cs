using ACE.Shared.Mods;

namespace PathChoice;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PathChoice), new PatchClass(this));
}
