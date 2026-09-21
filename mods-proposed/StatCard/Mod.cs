using ACE.Shared.Mods;

namespace StatCard;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(StatCard), new PatchClass(this));
}
