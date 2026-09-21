using ACE.Shared.Mods;

namespace HotspotAlert;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HotspotAlert), new PatchClass(this));
}
