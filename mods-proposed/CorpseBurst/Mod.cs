using ACE.Shared.Mods;

namespace CorpseBurst;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(CorpseBurst), new PatchClass(this));
}
