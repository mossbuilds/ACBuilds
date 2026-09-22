using ACE.Shared.Mods;

namespace PkStatusInfo;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PkStatusInfo), new PatchClass(this));
}
