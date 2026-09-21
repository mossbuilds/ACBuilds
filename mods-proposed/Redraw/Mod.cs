using ACE.Shared.Mods;

namespace Redraw;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(Redraw), new PatchClass(this));
}
