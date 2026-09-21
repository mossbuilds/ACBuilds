using ACE.Shared.Mods;

namespace AnnounceEvents;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AnnounceEvents), new PatchClass(this));
}
