# StuckVendorWatch (proposed, not deployed)

`/vendorwatch`, read-only, Sentinel-only, off by default (idea 54 in `IDEAS.md`).

## Why
Players report "the vendor won't sell/buy anything" with no server-side alert. `Vendor.OpenForBusiness` going
false and never recovering because its `ResetTimestamp`/`ResetInterval` cycle stalled is a known ACE-community
complaint pattern. This gives Sentinels a `/vendorwatch` command (and, if `Enabled`, a periodic whisper to online
Sentinels) that surfaces it without ever touching the vendor.

## Approach chosen: (a), reflection into the protected timer fields - not the honest OpenForBusiness-only fallback
The idea's own spec flagged a real problem up front: `VendorStock` (this repo, `mods-proposed/VendorStock`) already
proved by a real compile attempt that `Vendor.ResetTimestamp`/`ResetInterval` are real members of `Vendor.cs` but
**protected**, not public - unreachable from a mod without reflection.

Two honest options existed:
- **(a)** Harmony `Traverse` into the protected fields, same pattern `FellowshipShareToggle` already ships
  (`mods-proposed/FellowshipShareToggle/PatchClass.cs`) for its private-method calls, guarded in try/catch.
- **(b)** Drop the duration math entirely and only flag `OpenForBusiness == false` right now, calling that
  "currently closed" rather than "confirmed stuck" - the fallback `VendorStock` itself took for its (much more
  cosmetic) "seconds until next reset" nice-to-have.

**This mod takes (a).** Reasoning:
- The entire point of "StuckVendorWatch" as an idea is telling a stalled reset apart from a vendor closed by
  design. Without the timestamp math, `/vendorwatch` degrades to "list closed vendors," which is `OpenForBusiness`
  read directly with an extra command wrapped around it - not what was asked for, and not worth shipping as a
  mismatch with the sourced idea (the same standard `VendorStock`'s own README documents for a naming mismatch).
- The reflection here is strictly **lower-risk** than `FellowshipShareToggle`'s: it is two `Traverse.Field(...).GetValue<double>()`
  reads, never a method invocation, and nothing is ever written back to the vendor. If `ResetTimestamp` or
  `ResetInterval` is renamed or removed in a future ACE version, the read throws, is caught, is logged once per
  vendor, and that vendor's flag falls back automatically to the honest "closed right now, reset timer unreadable
  or not yet due" wording (see `SecondsPastDueReset` in `PatchClass.cs`) - it can never corrupt vendor state or
  crash the scan, unlike a failed reflected method call that leaves a mutation half-applied.
- **This is still flagged exactly as prominently as `FellowshipShareToggle` flags its own reflection risk**: this
  section, the code comment directly above `SecondsPastDueReset`, and the per-vendor fallback wording all say the
  same thing - this reads a protected field by reflection and could silently stop working on an ACE update, in
  which case the command keeps running but only as the (b) fallback (OpenForBusiness-only, worded as such) rather
  than failing loudly.

## A type mismatch found on review, before compiling
`Vendor.cs`'s own code (`var resetInterval = ResetInterval ?? 300; ResetTimestamp = Time.GetFutureUnixTime(resetInterval);`,
where `GetFutureUnixTime` takes a `double`) implies `ResetInterval` is nullable and only implicitly convertible to
`double` - almost certainly `int?`, not itself a `double`. The first draft read it with
`Traverse.Field("ResetInterval").GetValue<double>()`, which would throw on **every single call** (a boxed `int?`
cannot be cast straight to `double`), silently degrading this mod to the (b) fallback for every vendor, every time -
the exact case (a) exists to do better than. Fixed to `GetValue<int?>()` then converted to `double`.
**This fix compiles, but a successful compile cannot confirm it's runtime-correct**: `Traverse.GetValue<T>()` is
pure reflection, unchecked by the compiler against `T` regardless of what `T` is. Only running this against a real,
currently-closed vendor can confirm the field is genuinely `int?` and that the read actually succeeds rather than
falling back silently. Treat the "reset overdue by Ns" branch as unverified until that's been watched happen once.

## What `/vendorwatch` does
Scans every loaded landblock's `WorldObjects` for `Vendor` instances with `OpenForBusiness == false` (the same
`LandblockManager.GetLoadedLandblocks()` + per-landblock `WorldObjects` scan pattern already shipped in
`HotspotAlert`/`ServerPulse`). For each one, reflects into `ResetTimestamp`/`ResetInterval` to compute how many
seconds past its own reset deadline it is:
- Overdue (`now > ResetTimestamp + ResetInterval`): reported as "reset overdue by Ns (likely stalled)".
- Not overdue, or the reflected read failed: reported as "closed right now (reset timer unreadable or not yet
  due - could be by design)".

Every call to `/vendorwatch` also prints the false-positive caveat in the command's own output, per the idea's
instruction: a vendor legitimately closed by design (an event, a quest-gated shop) can appear here, and since this
mod is entirely read-only the cost of that false positive is only a wasted look - staff still fix a genuinely
stuck vendor by hand with the existing `/reload`-style stock commands.

## What it never does
No vendor is ever reset, reloaded, or otherwise touched - this is a read-only diagnostic, matching the idea's own
requirement. No `ace_auth`/`ace_shard` access. The periodic whisper (`ScanSeconds`, throttle matching HotspotAlert's
cadence, minimum 30s enforced) only fires when `Enabled` is true; `Enabled` defaults to `false`.

## Command-clash check
`vendorwatch` does not appear among the 327 built-in commands in `%TEMP%\cmds.txt`. `raise`, `unfreeze`, and
`resyncproperties` are built-ins (unrelated); `order` is not (also unrelated) - neither clashes with `vendorwatch`.
