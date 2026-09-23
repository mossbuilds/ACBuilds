using ACE.Shared.Mods;

namespace ThreatTableView;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ThreatTableView), new PatchClass(this));
}
