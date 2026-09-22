using ACE.Shared.Mods;

namespace BurdenCheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(BurdenCheck), new PatchClass(this));
}
