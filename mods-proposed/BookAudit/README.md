# BookAudit (proposed, not deployed)

Idea 81, Round 14. Sentinel-only moderation log for player-authored book content (add/modify/delete pages) - a griefing/harassment audit trail. Nothing in this mod blocks, alters, or rejects a write; it only records what already happened.

## What it does

Logs every add/modify/delete of a book page to `bookaudit.log` (server working dir, when `Enabled`): UTC timestamp, acting character name, action, book guid, page index (modify/delete), whether the underlying write actually succeeded, and the page text itself (add/modify, when `LogFullText` is true).

`/bookaudit [n]` (Sentinel+, max 50) or `/bookaudit <character name>` tails the log, same pattern as `TradeLedger`/`LootWatch`.

## What it does not do

- Never blocks, rejects, or alters a book write - Harmony **postfix** only, the real method always runs first and this mod only reads its result afterward.
- Does not touch inscriptions, book titles/short descriptions, or any object other than book pages.
- Does not record account names or IP/session data, only the acting character's in-game name.
- Off by default (`Enabled: false`).

## Hooks (re-verified this round against a fresh full-file fetch)

Patched on `ACE.Server.WorldObjects.Book` (`Source/ACE.Server/WorldObjects/Book.cs`), **not** `Player_Book.cs`'s `HandleActionBookAddPage`/`ModifyPage`/`DeletePage`, even though both were named in the idea and both are confirmed `public`:

- `public PropertiesBookPageData AddPage(uint authorId, string authorName, string authorAccount, bool ignoreAuthor, string pageText, out int index)`
- `public bool ModifyPage(int index, string pageText, Player player)`
- `public bool DeletePage(int index, Player player)`

Why `Book`'s own methods and not `Player`'s handlers: each already takes the acting identity as a direct parameter (`authorName` on `AddPage`; the `Player` object on `ModifyPage`/`DeletePage`), so patching here needs no second lookup, same as patching `Player` would have. But `Player_Book.cs`'s handlers **discard the real success/failure**: `HandleActionBookModifyPage` sends `GameEventBookModifyPageResponse(..., true)` unconditionally regardless of what `ModifyPage` actually returned, and `HandleActionBookDeletePage` at least forwards `DeletePage`'s bool but a postfix on the handler would have to re-derive it. Patching `Book`'s methods directly gives the true `bool __result` (or `null` result for `AddPage`) for free - this is exactly the "log should also capture the bool success... not assume every call succeeded" risk the idea flagged, resolved by choosing the deeper hook.

`AddPage` is always called by the client flow with `pageText: ""` (a book page is added blank, then filled by a separate `ModifyPage` call) - so "add" log lines showing empty text reflect the real client behavior, not a mod bug.

## Settings

- `Enabled` (default `false`)
- `LogFile` (default `bookaudit.log`)
- `MaxKb` / `MaxFiles` - rotation, same as `TradeLedger`
- `LogFullText` (default `true`) - when `false`, logs only the action and text length, not the written content, for servers that want metadata-only auditing

## Risks

- Log volume on a busy scribing server - rotates the same way every other file-log mod in this repo does (`MaxKb`/`MaxFiles`).
- `LogFullText: true` puts player-written free text into a plaintext log file on disk; treat it the same as any other moderation log (Sentinel-only command, server-side file).

## How to test (once compiled and hot-loaded, not part of this proposal)

1. Enable the mod and set `Enabled: true` in `Settings.json`.
2. As any player, scribe a blank book, add a page, modify it, then delete it.
3. As a Sentinel, run `/bookaudit 5` and confirm one line per action with the correct text and `success true`.
4. Have a second, non-author player attempt to modify/delete the first player's page (should fail unless `IgnoreAuthor` or Sentinel/Admin) and confirm the log shows `success False`.

## How to enable later

Flip `Enabled: true` in `Settings.json` (or `Meta.json` default) and restart/hot-reload. No other repo files are touched by this proposal.
