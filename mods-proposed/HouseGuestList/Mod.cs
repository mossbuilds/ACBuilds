using ACE.Shared.Mods;

namespace HouseGuestList;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HouseGuestList), new PatchClass(this));
}
