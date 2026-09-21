using ACE.Shared.Mods;

namespace IdleKick;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(IdleKick), new PatchClass(this));
}
