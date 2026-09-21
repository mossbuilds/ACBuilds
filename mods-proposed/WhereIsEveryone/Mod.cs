using ACE.Shared.Mods;

namespace WhereIsEveryone;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(WhereIsEveryone), new PatchClass(this));
}
