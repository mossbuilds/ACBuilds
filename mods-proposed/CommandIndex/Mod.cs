using ACE.Shared.Mods;

namespace CommandIndex;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(CommandIndex), new PatchClass(this));
}
