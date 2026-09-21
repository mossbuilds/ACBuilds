using ACE.Shared.Mods;

namespace PetSpells;

public class Mod : BasicMod
{
    public Mod() : base() => Setup(nameof(PetSpells), new PatchClass(this));
}
