using ACE.Shared.Mods;

namespace SettingsPeek;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(SettingsPeek), new PatchClass(this));
}
