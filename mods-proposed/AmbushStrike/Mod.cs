using ACE.Shared.Mods;

namespace AmbushStrike;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AmbushStrike), new PatchClass(this));
}
