using ACE.Shared.Mods;

namespace CraftForecast;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(CraftForecast), new PatchClass(this));
}
