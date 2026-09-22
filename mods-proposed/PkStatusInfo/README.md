# PkStatusInfo

Read-only `/pkstatus` (all players): your own `PlayerKillerStatus`, whether the PK timer / logout-freeze window is active, and roughly how many seconds remain. Off by default (`Enabled=false` in Settings.json and Meta.json). No settings beyond the on/off switch, no patches, pure property reads.

## Verified against ACE master (Source/ACE.Server/WorldObjects/Player_Combat.cs, WorldObject_Properties.cs)
- `PlayerKillerStatus` - public property (defined on `WorldObject`, `WorldObject_Properties.cs:2702`), inherited enum (`PK`/`PKLite`/`NPK`/`Free`/etc.). Confirmed.
- `LastPkAttackTimestamp` - public `double` property (`PropertyFloat.LastPkAttackTimestamp`). Confirmed.
- `PKTimerActive` - public `bool` property: `IsPKType && (now - LastPkAttackTimestamp) < PropertyManager.GetLong("pk_timer").Item`. Confirmed - **but it reads the `pk_timer` config key, not `pk_respite_timer`.**
- `PKLogoutActive` - public `bool` property: `IsPKType && (now - LastPkAttackTimestamp) < PKLogoffTimer.TotalSeconds`. Confirmed.
- The static timer field is `PKLogoffTimer` (`public static TimeSpan`, 2 minutes) - **the idea entry named it `PKLogoutTimer`, which does not exist; the real name is `PKLogoffTimer`.** Used as written (the correct name), not the idea's spelling.

## Corrected from the idea entry (don't take this at face value either - re-verify against the ACE version you build against)
- **`pk_timer` vs `pk_respite_timer`**: the idea's spec said compute remaining time "from `LastPkAttackTimestamp` plus the server's configured `pk_timer`/`pk_respite_timer`" as if they were interchangeable. They are not: `PKTimerActive` itself only ever reads `pk_timer` (a `long` key, read with `PropertyManager.GetLong`, same pattern as SettingsPeek). `pk_respite_timer` is a separate `double` key read elsewhere (`Player_Death.cs`, `PlayerManager.cs`) for a different check (the post-death respite message/gate), not for this flag. This mod reads `pk_timer` because that's what actually governs `PKTimerActive`.
- **`PKLogoutTimer` -> `PKLogoffTimer`**: field name in the idea was wrong (verified by grep against raw source, not by trusting the paraphrase). Fixed in code.

## Dropped from the idea (named in the entry but not used here)
- **`PkTimestamp`** - exists as a public `double` property, but nothing in `Player_Combat.cs` derives an active-window/remaining-seconds calculation from it (only `LastPkAttackTimestamp` feeds `PKTimerActive`/`PKLogoutActive`). Including it would mean inventing a display for a field the actual PK logic doesn't use for timing - dropped rather than forced in.
- **`pk_new_character_grace_period`** - a real, documented `PropertyManager` key (confirmed present in `PropertyManager.cs`'s defaults), but grepped for any use in `Player.cs`/`Player_Combat.cs` and found none - nothing adds it to a timestamp to produce a "grace period remaining" number. Showing one would be a guess dressed up as a read. Dropped; the command only reports what `PKTimerActive`/`PKLogoutActive` actually compute.

## Unverified
- Not compiled (per instructions) - a real build against the ACE binaries is the final check, same as the VendorStock/SettingsPeek lesson. `Session.Player`, `ACE.Common.Time.GetUnixTime()`, and `PropertyManager.GetLong` are used exactly as they appear elsewhere in ACE source, but accessibility can only be fully confirmed by compiling.
- Command name `pkstatus` checked against `%TEMP%\cmds.txt`: not a built-in (`raise`/`unfreeze`/`resyncproperties` are; `order` is not; `pkstatus` doesn't appear either).

## Test
Set `Enabled=true` in Settings.json and Meta.json, run `/pkstatus`.
