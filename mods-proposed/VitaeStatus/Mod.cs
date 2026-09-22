using ACE.Shared.Mods;

namespace VitaeStatus;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(VitaeStatus), new PatchClass(this));
}
