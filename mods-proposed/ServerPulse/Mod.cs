using ACE.Shared.Mods;

namespace ServerPulse;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ServerPulse), new PatchClass(this));
}
