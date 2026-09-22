using ACE.Shared.Mods;

namespace PKLiteZone;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PKLiteZone), new PatchClass(this));
}
