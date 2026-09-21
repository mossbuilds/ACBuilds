using ACE.Shared.Mods;

namespace RaiseSkeleton;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(RaiseSkeleton), new PatchClass(this));
}
