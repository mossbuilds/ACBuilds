using ACE.Shared.Mods;

namespace Leaderboard;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(Leaderboard), new PatchClass(this));
}
