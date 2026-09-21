using ACE.Shared.Mods;

namespace BugNote;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(BugNote), new PatchClass(this));
}
