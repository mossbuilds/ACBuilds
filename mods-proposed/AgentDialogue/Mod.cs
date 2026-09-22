using ACE.Shared.Mods;

namespace AgentDialogue;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AgentDialogue), new PatchClass(this));
}
