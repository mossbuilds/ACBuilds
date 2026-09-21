using ACE.Shared.Mods;

namespace TeleBack;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TeleBack), new PatchClass(this));
}
