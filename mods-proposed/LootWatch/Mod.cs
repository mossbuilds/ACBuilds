using ACE.Shared.Mods;

namespace LootWatch;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(LootWatch), new PatchClass(this));
}
