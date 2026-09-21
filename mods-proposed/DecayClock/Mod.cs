using ACE.Shared.Mods;

namespace DecayClock;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(DecayClock), new PatchClass(this));
}
