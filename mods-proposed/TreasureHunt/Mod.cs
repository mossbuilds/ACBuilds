using ACE.Shared.Mods;

namespace TreasureHunt;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TreasureHunt), new PatchClass(this));
}
