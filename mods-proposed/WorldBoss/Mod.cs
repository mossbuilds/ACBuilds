using ACE.Shared.Mods;

namespace WorldBoss;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(WorldBoss), new PatchClass(this));
}
