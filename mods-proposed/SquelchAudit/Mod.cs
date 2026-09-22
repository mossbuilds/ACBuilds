using ACE.Shared.Mods;

namespace SquelchAudit;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(SquelchAudit), new PatchClass(this));
}
