using ACE.Shared.Mods;

namespace TinkerHistory;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TinkerHistory), new PatchClass(this));
}
