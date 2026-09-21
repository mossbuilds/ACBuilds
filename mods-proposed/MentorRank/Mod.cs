using ACE.Shared.Mods;

namespace MentorRank;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(MentorRank), new PatchClass(this));
}
