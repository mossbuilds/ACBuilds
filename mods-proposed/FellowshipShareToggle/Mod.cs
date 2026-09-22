using ACE.Shared.Mods;

namespace FellowshipShareToggle;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(FellowshipShareToggle), new PatchClass(this));
}
