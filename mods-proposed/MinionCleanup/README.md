# MinionCleanup
Source only, not compiled or deployed. Companion to RaiseSkeleton: ACE tracks one pet per player (CurrentActivePet, destroyed in Player.LogOut_Inner), so extra minions would survive logout, death and long teleports.
Removes CombatPets whose PetOwner is a player and whose WeenieClassId is in MinionWcids (default 900021240) when the owner logs out (LogOut_Inner prefix), dies (Player.Die postfix), teleports (Player.Teleport postfix, checked 3 s later), or on a periodic sweep (owner offline/dying, other landblock, farther than MaxDistance). Pets are found by scanning LandblockManager.GetLoadedLandblocks() for CombatPet and reading PetOwner, so untracked pets are covered. Destroy runs via ActionChain on the pet. Nothing is spawned; there is no DB access. Destroy only (no recall).
Command: /minionsweep (Admin) runs a sweep now even if disabled; prints checked/removed counts.
Settings.json: Enabled false, MinionWcids [900021240], MaxDistance 60, SweepSeconds 10 (min 5), OnLogout/OnDeath/OnTeleport true.
Risks: sweep timer thread reads landblock object copies (GetAllWorldObjectsForDiagnostics returns a copy); Die patch is by-name reflection.
