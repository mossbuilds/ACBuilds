using ACE.Shared.Mods;

namespace DeathReport;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(DeathReport), new PatchClass(this));
}
