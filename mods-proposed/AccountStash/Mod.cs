using ACE.Shared.Mods;

namespace AccountStash;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(AccountStash), new PatchClass(this));
}
