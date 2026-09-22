using ACE.Shared.Mods;

namespace AgentActions;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AgentActions), new PatchClass(this));
}
