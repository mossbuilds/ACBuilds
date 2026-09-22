using ACE.Shared.Mods;

namespace GeneratorAudit;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(GeneratorAudit), new PatchClass(this));
}
