using ACE.Shared.Mods;

namespace RentReminder;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(RentReminder), new PatchClass(this));
}
