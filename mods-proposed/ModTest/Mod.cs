using ACE.Shared.Mods;

namespace ModTest;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ModTest), new PatchClass(this));
}
