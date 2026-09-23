# RecallCooldown

Optional shared per-player cooldown across every recall spell. **Off by default** (`Enabled=false`, `CooldownSeconds=0`): with either unset, the patch returns immediately and retail behaviour is unchanged.

## What it does
- Once a player has recalled, any further recall that targets them within `CooldownSeconds` is refused with a chat line: "You can recall again in N seconds."
- Closes the chain-recall gap: stock ACE throttles recalls only by each spell's own 2-second `AddDelaySeconds(2.0f)` animation delay, so Lifestone Recall -> Portal Recall -> Portal Tie Recall can be cast back to back to escape a fight or on a corpse run.
- Admin `/recallcooldown [prune]` shows the settings, how many players are tracked, and how many recalls have been blocked since startup. `prune` drops expired entries.

## What it does not do
- It does not change summon, sending, or portal-use spells: `HandleCastSpell_PortalSummon`, `PortalSending`, `FellowPortalSending`, and walking through a portal are separate paths and are left alone.
- It does not refund components or mana on a blocked recall (see risks).
- It writes nothing to the database. Cooldowns live in memory and reset on a restart.

## How recall works in ACE (verified against ACEmulator/ACE master)
- `Source/ACE.Server/WorldObjects/WorldObject_Magic.cs`
  - line 259: `protected bool HandleCastSpell(...)` switches on `spell.MetaSpellType`. Lines 313-315 (`case SpellType.PortalRecall:`) call `HandleCastSpell_PortalRecall(spell, targetCreature)`.
  - line 1046: `private void HandleCastSpell_PortalRecall(Spell spell, Creature targetCreature)`. It is private, so the patch is by name: `[HarmonyPatch(typeof(WorldObject), "HandleCastSpell_PortalRecall", new[] { typeof(Spell), typeof(Creature) })]`.
  - Its switch covers `PortalRecall`, `LifestoneRecall1`, `LifestoneSending1` (the sanctuary sending), `PortalTieRecall1` and `PortalTieRecall2`. So every recall does go through one method and the shared cooldown does not have to be pieced together from several places. `LifestoneSending1` shares the cooldown too.

## Gating-safety analysis
- **Where the prefix runs.** The prefix runs before the first line of the method, which comes before the Olthoi and PK-timer checks and before any `ActionChain` is built. When it returns `false`, `DoPreTeleportHide()` never runs, no 2-second delay is queued, and no teleport happens. The player stays where they are, visible and in control. Nobody is left hidden or halfway through a teleport.
- **Components and mana.** Both are spent earlier in the cast pipeline (`Player_Magic.cs`, as ComponentPrecheck found), before `HandleCastSpell` is ever called. A blocked recall therefore uses them up. This is the same thing that already happens on the method's own stock early returns: the Olthoi error, `YouHaveBeenInPKBattleTooRecently`, `YouMustLinkToPortalToRecall`/`...Lifestone...`, `YouCannotRecallPortal`, and a failed `portal.CheckUseRequirements`. The mod adds one more reason for a failure that ACE already handles. It does not create a new kind of partial state. Items are not consumed twice, because the method never touches inventory.
- **Where the cooldown is recorded.** It is stamped in the prefix when a recall is allowed through. If the original method then fails for its own reasons (no linked portal, PK timer), the cooldown still starts. That is conservative: it can wrongly block a player for up to `CooldownSeconds`, but it can never let an extra recall through. Keep `CooldownSeconds` modest.
- **Whose cooldown.** The key is `targetCreature` as a Player, meaning the person being moved. Recalls are self-targeted, so this is the caster. A cast where the target is not a player (for example an NPC) always passes through.
- **Faults.** The whole prefix is wrapped in try/catch. On any exception it logs and returns `true`, so a bug in the mod can never block a recall.
- **Fragility.** Because the patch is by name, a future ACE rename or signature change makes Harmony fail at patch time, and the server logs it. It does not silently misbehave. Recheck line 1046 when you upgrade ACE.
- **The existing PK-timer rule still applies.** The cooldown is added on top of ACE's own `PKTimerActive` block. `ExemptInPk=true` skips the cooldown for PK-flagged players only.

## Settings
| Key | Default | Meaning |
|---|---|---|
| Enabled | false | Master switch |
| CooldownSeconds | 0 | Shared cooldown in seconds. 0 means no gating. |
| ExemptInPk | false | When true, PK-status players are never gated |
| PruneSeconds | 300 | How often expired entries are swept out of memory |

## How to test (local server only)
1. Set `Enabled=true` and `CooldownSeconds=60`. Link a lifestone and a portal.
2. Cast Lifestone Recall. You should teleport normally.
3. Straight away, cast Portal Recall. You should see the "recall again in N seconds" line, stay in place and fully visible, and lose that cast's components, just as you would for "must link a portal".
4. Wait 60 seconds and recall again. It should work.
5. Run `/recallcooldown` and check the tracked and blocked counts. Set `Enabled=false` and confirm back-to-back recalls work again.

## Enable later
Copy the folder to `mods/`, rebuild the image, and set `Enabled`/`CooldownSeconds` in Settings.json. None of this has been done here.
