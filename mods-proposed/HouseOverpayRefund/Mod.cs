using ACE.Shared.Mods;

namespace HouseOverpayRefund;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(HouseOverpayRefund), new PatchClass(this));
}
