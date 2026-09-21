using ACE.Shared.Mods;

namespace TimedMute;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(TimedMute), new PatchClass(this));
}
