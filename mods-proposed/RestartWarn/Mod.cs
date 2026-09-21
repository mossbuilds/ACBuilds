using ACE.Shared.Mods;

namespace RestartWarn;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(RestartWarn), new PatchClass(this));
}
