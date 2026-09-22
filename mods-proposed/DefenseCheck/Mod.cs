using ACE.Shared.Mods;

namespace DefenseCheck;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(DefenseCheck), new PatchClass(this));
}
