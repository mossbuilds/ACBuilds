using ACE.Shared.Mods;

namespace SpellFizzleForecast;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(SpellFizzleForecast), new PatchClass(this));
}
