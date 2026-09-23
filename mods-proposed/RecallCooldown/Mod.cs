using ACE.Shared.Mods;

namespace RecallCooldown;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(RecallCooldown), new PatchClass(this));
}
