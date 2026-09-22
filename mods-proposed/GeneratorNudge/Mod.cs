using ACE.Shared.Mods;

namespace GeneratorNudge;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(GeneratorNudge), new PatchClass(this));
}
