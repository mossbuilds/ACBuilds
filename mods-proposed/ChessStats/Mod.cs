using ACE.Shared.Mods;

namespace ChessStats;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ChessStats), new PatchClass(this));
}
