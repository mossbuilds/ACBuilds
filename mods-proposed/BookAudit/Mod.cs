using ACE.Shared.Mods;

namespace BookAudit;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(BookAudit), new PatchClass(this));
}
