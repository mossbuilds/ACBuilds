using ACE.Shared.Mods;

namespace LoginGreeter;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(LoginGreeter), new PatchClass(this));
}
