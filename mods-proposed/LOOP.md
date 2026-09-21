# mods-proposed: the mod-writing loop

Goal: keep producing well-researched ACE mods as SOURCE ONLY. **Nothing here is deployed.** Tom reviews and decides.

## Hard rules
- Write mods only under `C:\files\ACBuilds\mods-proposed\<Name>\`. Never touch `mods/`, `Dockerfile.server`, `server/entrypoint.sh`, the VPS, or the running servers. Never `git push`. Committing locally is fine.
- Do not hot-load, copy to `/opt/acbuilds/...`, or run console commands against the servers.
- Each mod is a folder with the same layout as `mods/ACBuildsAdmin` (csproj, Mod.cs, PatchClass.cs, Settings.cs, GlobalUsings.cs, Meta.json) plus a README.md (what it does, commands, settings, risks, how to test, how to enable later).
- Every mod must compile: run `mods-proposed/check-mod.sh <Name>` (delegate it to the Haiku verifier). Only mark it READY when it prints `MOD OK`.
- Client limits: no speed hacks (client ignores server velocity); avoid mods that add many world objects (headset memory).
- Player-affecting commands are `AccessLevel.Admin` unless the idea is clearly a normal-player feature; console-only commands use `CommandHandlerFlag.ConsoleInvoke`.

## Each run
1. Read `IDEAS.md` (ranked list) and `STATUS.md`. Take the highest-ranked idea not yet in STATUS.md.
2. Verify the ACE APIs you will use against raw.githubusercontent.com/ACEmulator/ACE/master/Source/... (download the file, grep). Do not guess method names.
3. Write the mod. Build it with check-mod.sh (Haiku agent). Fix compile errors until `MOD OK`.
4. Append a line to STATUS.md: `Name | READY (compiled) or WIP (why) | date | one-line summary`. Commit locally in C:\files\ACBuilds.
5. Stop after ONE mod per run. If IDEAS.md is exhausted, research 5 more ideas and append them.
