using ACE.Shared.Mods;

namespace ImbueInspect;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ImbueInspect), new PatchClass(this));
}
