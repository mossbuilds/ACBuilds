using ACE.Shared.Mods;

namespace TriviaNight;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TriviaNight), new PatchClass(this));
}
