using ACE.Shared.Mods;

namespace AgentFeed;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AgentFeed), new PatchClass(this));
}
