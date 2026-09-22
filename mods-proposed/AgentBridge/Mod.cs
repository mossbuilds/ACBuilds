using ACE.Shared.Mods;

namespace AgentBridge;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AgentBridge), new PatchClass(this));
}
