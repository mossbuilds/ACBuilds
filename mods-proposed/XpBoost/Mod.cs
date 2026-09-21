using ACE.Shared.Mods;

namespace XpBoost;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(XpBoost), new PatchClass(this));
}
