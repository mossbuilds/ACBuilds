using ACE.Shared.Mods;

namespace MinionFollow;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(MinionFollow), new PatchClass(this));
}
