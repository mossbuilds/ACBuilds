using ACE.Shared.Mods;

namespace RareTally;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(RareTally), new PatchClass(this));
}
