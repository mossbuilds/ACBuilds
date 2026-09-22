using ACE.Shared.Mods;

namespace ItemSetPreview;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(ItemSetPreview), new PatchClass(this));
}
