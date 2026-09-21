# CommandIndex

Read-only `/modhelp` (all players). `/commands`, `/cmds`, `/mods`, `/serverhelp`, `/helpme` checks: `/modhelp` and the others are not in the 327 built-ins (`/mods` was avoided as ACE.BaseMod uses a mod verb). Off by default (`Enabled=false` in Settings.json and Meta.json).

## Lists
`CommandManager.GetCommands()` (ACE.Server.Command, public static) filtered to names in `Allowlist` AND `Attribute.Access <= caller's Session.AccessLevel`; prints `Attribute.Description` or a `DescriptionOverrides` text. Commands not registered (mod not installed) simply do not appear. No admin or hidden command can leak. No files, no DB, no world objects.

## Unverified
Whether every mod's Description is filled in (else override it). Not compiled. Dictionary is unsynchronised in ACE; the copy retries on InvalidOperationException.
