using ACE.Shared.Mods;

namespace BuffBot;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(BuffBot), new PatchClass(this));
}
