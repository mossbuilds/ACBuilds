using ACE.Shared.Mods;

namespace EnlightenmentReady;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(EnlightenmentReady), new PatchClass(this));
}
