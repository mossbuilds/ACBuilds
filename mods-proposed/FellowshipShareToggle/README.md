# FellowshipShareToggle

**Gap confirmed: yes.** ACE has no built-in player command that changes fellowship XP sharing after the
fellowship is created. The three built-in `fellow`-prefixed commands (checked against `%TEMP%\cmds.txt`
and against `Command/Handlers/PlayerCommands.cs`) are `fellow-info` and `fellow-dist` (admin/sentinel
debug dumps of fellowship internals) and `fellowbuff` (unrelated). The player-facing "interaction
settings" list (`AutoAcceptFellowRequest`, `FellowshipShareLoot`, `FellowshipShareXP`, etc., set via
`/isettings` or similar) only sets the *character option* `ShareFellowshipExpAndLuminance`, which is
read once when *you* create a new fellowship (`Player.FellowshipCreate(string, bool shareXP)`) - it does
not touch a fellowship you're already in. There is no other command, character option, or admin tool
that flips an existing fellowship's sharing. The only workaround today is disbanding and re-forming the
fellowship, losing everyone's membership in the process.

## Command

`/fellowshare <on|off|status>`. Off by default. Leader-only for `on`/`off`; any member can check
`status`.

- `status` - prints `DesiredShareXP` (what the leader wants) plus the currently-active `ShareXP`/
  `EvenShare` (what ACE's own level-spread rule currently allows).
- `on` / `off` - leader-only (`FellowshipLeaderGuid == caller`, same check
  `Player_Fellowship.HandleActionFellowshipChangeOpenness` uses for openness/lock changes). Sets
  `Fellowship.DesiredShareXP` and immediately recomputes/broadcasts.

## Data read and changed

Everything is on the caller's own `Player.Fellowship` (`ACE.Server.Entity.Fellowship`), verified against
raw `Entity/Fellowship.cs`:

- `DesiredShareXP` (public `bool` field) - **written directly**, no reflection needed. It is set once in
  the `Fellowship(Player leader, string fellowshipName, bool shareXP)` constructor and never reassigned
  anywhere else in ACE.
- `ShareXP` / `EvenShare` (public `bool` fields, read-only from outside) - recomputed by
  `CalculateXPSharing()`, a **private** method with no public equivalent. Invoked here via Harmony's
  `Traverse.Create(fs).Method("CalculateXPSharing").GetValue()` - the same reflection pattern this repo
  already uses for private/protected ACE members it does not own (e.g. AmbushStrike's
  `AttackHook`/`DamageEvent` access).
- `UpdateAllMembers()` (also private) - called the same way immediately after, so every online fellow's
  client gets a `GameEventFellowshipFullUpdate` right away instead of waiting for the next membership
  change to pick up the new value.
- `FellowshipLeaderGuid` (public `uint` field) - read only, for the leader check.

`EvenShare` is never written directly, only ever recomputed by ACE's own `CalculateXPSharing()` - this
mod does not touch the even/weighted split rule itself, exactly as the idea's risk note requires.

## Settings

- `Enabled` (bool, default `false`) - master switch.
- `CooldownSeconds` (int, default `10`) - minimum time between two successful toggles for the *same*
  fellowship (tracked in a static in-memory dictionary keyed by the `Fellowship` object reference, cleared
  on server restart), to stop on/off spam mid-fight. `status` is never rate-limited.

## Risks

- `CalculateXPSharing` and `UpdateAllMembers` are called by name through reflection; if a future ACE
  version renames or removes either, the call throws inside a try/catch that logs
  `[FellowshipShareToggle] reflection call failed...` and tells the player nothing changed, rather than
  crashing the command pipeline or silently pretending the toggle worked.
- Setting `DesiredShareXP = true` does not guarantee `ShareXP` becomes `true` - ACE's own level-spread
  rule in `CalculateXPSharing()` can still leave sharing off (fellows too far apart in level). The command
  prints a note when this happens so it isn't mistaken for a bug.
- The cooldown dictionary is per-process and unbounded for the life of the server (one entry per distinct
  `Fellowship` object ever toggled); fellowships are transient (GC'd once disbanded and dereferenced) so
  this is not expected to grow unbounded in practice, but it is not explicitly pruned.
- Not verified: whether any other private ACE code path calls `CalculateXPSharing()`/`UpdateAllMembers()`
  with side effects beyond what's read in `Fellowship.cs` (e.g. logging, metrics) that this reflection
  call would also trigger - only the body shown in the source read this round was checked.

## Not done

No admin/other-fellowship variant - leader-only, own fellowship only, matching every other command in
this repo's fellowship mods (FellowshipPulse). Does not touch `ShareLoot` (a separate field, out of scope
for this idea) or `EvenShare` directly.
