using ACE.Shared.Mods;

namespace AdminAudit;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AdminAudit), new PatchClass(this));
}
