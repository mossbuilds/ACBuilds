using ACE.Shared.Mods;

namespace ComponentPrecheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ComponentPrecheck), new PatchClass(this));
}
