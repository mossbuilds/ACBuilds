using ACE.Shared.Mods;

namespace FellowshipPulse;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(FellowshipPulse), new PatchClass(this));
}
