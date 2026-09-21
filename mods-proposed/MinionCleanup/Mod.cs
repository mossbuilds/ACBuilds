using ACE.Shared.Mods;

namespace MinionCleanup;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(MinionCleanup), new PatchClass(this));
}
