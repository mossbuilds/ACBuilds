using ACE.Shared.Mods;

namespace HouseEligibilityCheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HouseEligibilityCheck), new PatchClass(this));
}
