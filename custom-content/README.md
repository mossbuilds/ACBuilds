# custom-content

Game content made for the ACBuilds servers, as ACE SQL. **This folder is the source of truth**: the world database is not in the nightly
backup and is replaced if the database image is recreated, so everything here can be re-applied at any time.

Layout mirrors ACE's own Content folder, so the files also work with the in-game `/import-sql` command:

    sql/weenies/<wcid 5 digits> <Name>.sql      items, creatures, NPCs, vendors, portals, generators, books...
    sql/landblocks/<landblock id>.sql            placed instances
    sql/quests/  sql/recipes/  sql/spells/  sql/events/

New content uses wcids from 90,000,000 up. Create and apply it with the Moss `ac-creator` skill (`~/.claude/skills/ac-creator`):

    python tools/ac_apply.py check  local sql/weenies        # lint + trial import, changes nothing
    python tools/ac_apply.py apply  local sql/weenies        # this PC's server
    python tools/ac_apply.py apply  vps   sql/weenies        # production (ask first)

Re-apply everything after the database is recreated: `ac_apply.py apply vps custom-content --overwrite --allow-low-wcid`.
Do not put third-party data (the Crossroads of Dereth location XML files) or secrets here; this repository is public.
