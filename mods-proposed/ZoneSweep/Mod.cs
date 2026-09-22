using ACE.Shared.Mods;

namespace ZoneSweep;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ZoneSweep), new PatchClass(this));
}
