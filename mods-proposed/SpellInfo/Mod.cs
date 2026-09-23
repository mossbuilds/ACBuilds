using ACE.Shared.Mods;

namespace SpellInfo;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(SpellInfo), new PatchClass(this));
}
