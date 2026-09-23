# Custom DAT utility — design note

Read-only research. No code written, no server or database touched. Written 2026-09-23 by Moss.

## Why

Every ACBuilds content addition so far has been limited to *existing* client DAT content, because ACBuilds
ships no game data (see `README.md`: "Nothing copyrighted is ever stored in our images" / "you supply your
own DAT files") and the ACE server on the VPS reads its DAT-derived tables (spell definitions, landblocks,
setups) from the same files each player already owns. `docs/NECROMANCER_SPELLS.md` is the sharpest example:
thirteen designed necromancer abilities had to be down-mapped onto 6266 pre-existing spell rows because ACE
"cannot add new spell visuals/icons." A DAT-patching utility removes that ceiling — new spell names,
descriptions, icons, and eventually models/dungeons — while preserving the "we ship no Turbine data" rule,
by patching each player's *own* DAT copy locally, and the server's separately-supplied copy, from the same
small, versioned patch.

## The four repos

| Repo | What it is | Language | License | Last activity | Reads DAT | Writes DAT | Usable as base? |
|---|---|---|---|---|---|---|---|
| [Serafino97/Seedsow](https://github.com/Serafino97/Seedsow) | Not a DAT tool — a full C++ AC emulator server fork (GDL-Classic/ClassicDereth lineage) for the old Dark Majesty-era client. README just links Mega.nz DAT downloads and a "portal.dat patcher" (unspecified, no source in-repo). | C++ | none asserted (NOASSERTION; upstream ClassicDereth has its own license) | 2018-06-22 (dead) | n/a | n/a (external patcher not in repo) | **No.** Wrong client era (pre-ToD), wrong purpose (server, not a DAT editor), unlicensed, dead 8 years. Likely a mis-pointed reference — worth telling Tom. |
| [derandark/DungeonViewerAC](https://github.com/derandark/DungeonViewerAC) | Standalone dungeon/cell viewer, renders `portal.dat`/`cell.dat` (old) or `client_portal.dat`/`client_cell_1.dat` (ToD) landblocks, objects, particle effects via the BSP tree. | C++ (MFC/DirectX era, pre-2004 codebase) | GPL-3.0 | 2017-02-01 (dead) | Yes — portal/cell (landblocks, env cells, BSP, particle scripts) | No (viewer only) | **No as a library** (C++, no build system shown, unmaintained, view-only). Useful only as a reference for cell/BSP layout semantics if we ever get to dungeon patching (phase 3+). |
| [ACEmulator/ACViewer](https://github.com/ACEmulator/ACViewer) | The official ACE-project DAT browser/viewer — objects, textures, models, spells, strings across all four EOR DAT files. Actively maintained (part of the ACEmulator org ACE itself depends on). States its own goal as "eventual DAT file editing... are goals for this project" — i.e. still read-only today. | C# (.NET Core/Framework, VS2017-era but actively pushed) | GPL-3.0 | 2026-08-13 (**active**, 38 stars) | Yes — portal (spells, setups, motion tables, regions), cell (landblocks), local (strings), highres | **Not yet** (editing is a stated future goal, not shipped) | **Good reference, not a base.** GPL-3.0 is copyleft — embedding its code in our tool would force our patch tool GPL; better to consult its object model/UI conventions without linking it. |
| [Chorizite/DatReaderWriter](https://github.com/Chorizite/DatReaderWriter) | Purpose-built open-source **read/write** library for EOR client DAT files (`client_portal.dat`, `client_cell_1.dat`, `client_local_English.dat`, `client_highres.dat`), with typed DB objects, BTree read/insert/remove, an async API, and a README example titled "Update spell names and descriptions" — i.e. exactly our use case, out of the box. NuGet package `Chorizite.DatReaderWriter`. | C# targeting `net8.0`, `netstandard2.0`, `net48` | **MIT** | 2026-08-29 (**active**, 4 stars) | Yes — Portal (spells, setups, GfxObj, MotionTable, Region), Cell, Local (strings), HighRes (icons/textures) | **Yes** — explicit BTree insertion/removal, confirmed by its own README code samples for both spell-text edits and MotionTable rewrites | **Best base.** MIT license (no copyleft), active, matches our exact target file set, permissively licensable as a NuGet dependency. |

## Recommendation: build on DatReaderWriter

`Chorizite/DatReaderWriter` is the only one of the four that both reads *and* writes the exact DAT files we
need, is actively maintained, and carries a license (MIT) compatible with a closed or open ACBuilds tool
either way. ACViewer and DungeonViewerAC are read-only or view-only and useful only as design references
(ACViewer's UI/data model, DungeonViewerAC's old cell/BSP notes if we ever reach dungeon editing). Seedsow is
not a DAT tool at all and is worth flagging back to Tom as likely a wrong pointer — its "portal.dat patcher"
link is an off-repo Mega.nz download with no source, era-mismatched (Dark Majesty, not Throne of Destiny),
and not something we can build on or trust.

Both ACE (the server, .NET 10) and our own ACBuilds tooling are .NET already, and DatReaderWriter targets
`net8.0` (loads fine under .NET 10) — a CLI utility in our repo (`tools/acdatpatch/` or similar), consuming
the `Chorizite.DatReaderWriter` NuGet package, is a natural fit and needs no new toolchain.

## 1. What to customize first, and why

Priority order, cheapest-to-verify first:

1. **Spell names, descriptions, and icons for the necromancer path.** `docs/NECROMANCER_SPELLS.md` already
   did the mapping work of "which existing spell ids can carry necromancer abilities" — the DAT patch turns
   that mapping into "give spell id X the name 'Corpse Explosion', the description text, and a bone-pile
   icon" instead of reusing an unrelated spell's cosmetics. This is the README's own worked example
   (`DatReaderWriter`'s "Update spell names and descriptions" sample) — smallest possible slice, string-table
   + one field write, no new geometry.
2. **New icons more generally** (item icons, not just spell icons) — same `client_highres.dat` write path,
   unblocks custom weapons/armor art without new spell mechanics.
3. **Later: models/setups** (new GfxObj/Setup entries for custom creatures or gear silhouettes) — bigger
   asset pipeline (needs 3D authoring, not just text/icon edits).
4. **Later still: landblocks/dungeon cells** — the AWAKEN_PORT_PLAN's later phases (custom dungeons) would
   need this; DungeonViewerAC's BSP notes become relevant only here.

## 2. Library and tool shape

- **Library:** `Chorizite.DatReaderWriter` (MIT, NuGet) — confirmed read+write above.
- **Tool:** a small .NET 10 CLI in this repo, e.g. `tools/acdatpatch/`, mirroring the ACE server's own
  runtime so no new SDK install is needed on the build machine or the VPS. Two subcommands to start:
  - `acdatpatch apply --dats <dir> --patch <patch-folder>` — apply one patch set to a local DAT install.
  - `acdatpatch verify --dats <dir> --patch <patch-folder>` — dry-run diff/hash check, no write.
  Later: `acdatpatch revert` (restore from the pre-patch backup copy `apply` always makes first).

## 3. Patch format

A **patch set** = one small, git-versioned folder under (e.g.) `dat-patches/necro-spell-icons-v1/`:

```
dat-patches/necro-spell-icons-v1/
  manifest.json        # patch id, version, target DAT iteration(s), description, author, date
  spells.json          # [{ "spell_id": 1234, "name": "Corpse Explosion", "description": "..." }, ...]
  icons/
    0x06001234.png      # new icon, named by the DAT file id it becomes
  strings.json          # any client_local_English string-table overrides
```

- **JSON + loose asset files**, not a binary DAT diff — reviewable in a PR like any other content change,
  matching how `ac-creator`/weenie SQL changes are already reviewed in this repo.
- `manifest.json` pins the **DAT iteration number** the patch was authored against (see §5) so `apply` can
  refuse or warn on a mismatched client.
- The same patch folder is applied twice: once by the **player's launcher** against their local DAT copy,
  once by **our deploy step** against the server's `/opt/acbuilds/dats` copy — one source of truth, two
  targets, never a hand-edited divergence.
- **Copyright flag:** the patch folder itself contains *no* Turbine asset bytes except the *new* content we
  authored (new icon PNGs, new strings) — it only *references* existing spell/file ids to retarget. This
  keeps ACBuilds' "we redistribute nothing of Turbine's" line intact: the patch is analogous to a mod diff,
  applied against DAT files the player already legally owns, never a redistributed DAT.

## 4. Distribution and rollback

- **Launcher side** (`launcher/acblauncher.py`, stdlib-only Python today): before starting `acclient.exe`,
  fetch the patch manifest (from our GHCR release or a repo raw URL), compare its version/hash against a
  small local state file (e.g. `dats/.acbuilds-patch-state.json`), and if newer:
  1. Back up the touched DAT(s) (copy-on-first-patch, keep exactly one pristine backup).
  2. Run `acdatpatch apply` (or call DatReaderWriter directly from a bundled .NET tool — the current launcher
     is pure Python stdlib, so this is the one place we'd need to either shell out to a published CLI binary
     or accept a Python DAT-writer port; shelling out to a self-contained `acdatpatch` exe is simplest and
     keeps the "no Python needed" self-contained launcher story intact).
  3. Verify by hash after writing; on mismatch, restore the backup and refuse to launch with a clear error.
- **Rollback:** `acdatpatch revert` (or the launcher's "Uninstall"-style button) restores the pristine backup
  — same safety pattern the repo already uses for DB backups in `acb.sh`/`acb.ps1` ("every update takes a
  backup first and stops if the backup fails").
- **Server side:** the VPS's `/opt/acbuilds/dats` gets the same patch applied as a deploy step (extend
  `docs/DEPLOY.md`'s existing deploy sequence: backup DB → also snapshot/patch the server DAT copy → restart
  `ace-server`), so the world DB's spell-table load and the DAT-derived cosmetics agree.
- **What breaks on mismatch:** if client and server disagree on a patched spell id's *name/description*,
  it's cosmetic only (the client shows the label, ACE keys spells by numeric id) — low risk. If a future
  patch touches anything ACE reads structurally from the DAT (e.g. a Setup/GfxObj id it validates, or a
  landblock the world DB references), a client/server DAT mismatch can desync visuals from mechanics or
  cause the client to reject/mis-render content the server thinks exists — this is the reason phase 1
  deliberately stays in "cosmetic only" territory (name/description/icon), where a mismatch is at worst
  a wrong label, never a crash or desync.

## 5. Compatibility risks

- **Per-client:**
  - **Retail client** — the primary target; DatReaderWriter is built against retail EOR DAT format, matches.
  - **OpenAC** — ships no game data either (per our own README) and is a from-scratch renderer; unknown
    whether it parses the *exact* same DAT byte layout DatReaderWriter writes (beta software, cross-platform,
    MIT). Needs a real compatibility check before shipping a patch that touches OpenAC players — start by
    patching only retail/AC:VR and gate OpenAC out until verified.
  - **AC:VR (Thwargle client)** — a retail-client derivative; likely DAT-format-compatible with retail since
    it reads the same `client_*.dat` files, but unverified — same "verify before shipping to this client"
    caution.
- **DAT iteration numbers:** DatReaderWriter's own `DatCollection` exposes `Portal.Iteration.CurrentIteration`
  — the patch manifest must record and check this so `apply` fails loudly rather than silently corrupting an
  unexpected DAT version (players may be on slightly different client patch levels).
- **ACE server caching:** ACE loads world data (spell table, weenies) from `ace_world` MySQL at startup/on
  demand, not live from the DAT — so a DAT icon/name change needs no server restart, only a client-side
  reload; but any DAT change ACE *does* read directly (if any — needs confirming against the ACE source,
  out of scope for this note) would need a server restart after redeploying the patched server-side DAT.

## 6. Phased plan

| Phase | Deliverable | Rough effort |
|---|---|---|
| 0 | Confirm OpenAC/AC:VR DAT-format compatibility empirically (patch a throwaway DAT copy, load it in each client, screenshot) | 0.5–1 day |
| 1 | **Smallest useful step:** rename one existing spell end-to-end — pick one necromancer-mapped spell id from `NECROMANCER_SPELLS.md`, write a `dat-patches/` folder with just `spells.json` (name + description, no icon), a manual `acdatpatch apply` run against a local test DAT copy, verify in retail client | 1–2 days (mostly `acdatpatch` CLI + DatReaderWriter plumbing; the "patch" content itself is trivial) |
| 2 | Add icon swap to the same patch (client_highres.dat write), same spell | 0.5–1 day |
| 3 | Wire `acdatpatch verify`/hash-check into the launcher's pre-launch flow + server deploy step in `docs/DEPLOY.md` | 1–2 days |
| 4 | Roll out full necromancer spell/icon set (13 abilities from `NECROMANCER_SPELLS.md`) as one versioned patch | 1–2 days (content authoring, not tooling) |
| 5 | Custom item icons more broadly (weapons/armor) | ongoing, same pipeline |
| 6 | Models/setups, then landblocks/dungeons | separate, larger design pass — needs 3D asset pipeline decisions, revisit DungeonViewerAC's BSP notes then |

## Copyright note (flagged explicitly per Tom's ask)

The patch-file approach keeps ACBuilds' current posture — "nothing copyrighted is ever stored in our
images/repo" — intact, because the versioned patch folder in our repo contains only *our own* new content
(new icon art, new text) plus *references* (numeric ids) into DAT files the player already owns and supplies
themselves; it never contains or redistributes bytes copied out of Turbine's DAT files. This should still get
a one-line sign-off from Tom before phase 1 ships, since it's a new category of "we touch the player's game
files," even though we already do exactly that today by pointing the DAT folder at their existing install.
