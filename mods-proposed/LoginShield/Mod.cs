using ACE.Shared.Mods;

namespace LoginShield;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(LoginShield), new PatchClass(this));
}
