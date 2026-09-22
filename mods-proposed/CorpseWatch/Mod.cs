using ACE.Shared.Mods;

namespace CorpseWatch;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(CorpseWatch), new PatchClass(this));
}
