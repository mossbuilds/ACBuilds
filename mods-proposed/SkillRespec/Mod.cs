using ACE.Shared.Mods;

namespace SkillRespec;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(SkillRespec), new PatchClass(this));
}
