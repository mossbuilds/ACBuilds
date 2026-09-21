using ACE.Shared.Mods;

namespace KillRace;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(KillRace), new PatchClass(this));
}
