using ACE.Shared.Mods;

namespace FellowshipRejoinWindow;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(FellowshipRejoinWindow), new PatchClass(this));
}
