using ACE.Shared.Mods;

namespace ACBuildsAdmin;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ACBuildsAdmin), new PatchClass(this));
}
