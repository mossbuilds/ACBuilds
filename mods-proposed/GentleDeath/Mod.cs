using ACE.Shared.Mods;

namespace GentleDeath;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(GentleDeath), new PatchClass(this));
}
