using ACE.Shared.Mods;

namespace HouseHookMeter;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HouseHookMeter), new PatchClass(this));
}
