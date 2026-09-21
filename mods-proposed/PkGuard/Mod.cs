using ACE.Shared.Mods;

namespace PkGuard;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PkGuard), new PatchClass(this));
}
