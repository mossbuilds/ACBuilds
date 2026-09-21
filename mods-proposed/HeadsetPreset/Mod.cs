using ACE.Shared.Mods;

namespace HeadsetPreset;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HeadsetPreset), new PatchClass(this));
}
