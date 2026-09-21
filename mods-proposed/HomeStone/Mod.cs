using ACE.Shared.Mods;

namespace HomeStone;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HomeStone), new PatchClass(this));
}
