using ACE.Shared.Mods;

namespace EventClock;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(EventClock), new PatchClass(this));
}
