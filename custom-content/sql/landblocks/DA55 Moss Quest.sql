/* Shoushi (0xDA55) is heavy for standalone headsets, so only a minimum of ours is placed here, each at Tom's request:
   Moss the Watchman (patrol NPC, 2026-09-22) and ONE Moving Target Drudge on a 2-minute respawn generator (2026-09-23).
   Place anything else (the Traveler, the rat warren) elsewhere with /createinst. Re-applying this file is safe. */
DELETE FROM `landblock_instance_link` WHERE `parent_GUID` = 0x7DA55A04;
DELETE FROM `landblock_instance` WHERE `guid` IN (0x7DA55A01, 0x7DA55A02, 0x7DA55A03, 0x7DA55A04, 0x7DA55A05);

INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`)
VALUES (0x7DA55A03, 900021242, 0xDA55001D, 84.800003, 99.000000, 20.000000, 1.0, 0, 0, 0.0, False, '2026-09-22 00:00:00') /* Moss the Watchman, patrol NPC */
     , (0x7DA55A04, 24129, 0xDA55001C, 93.386826, 86.979492, 20.011999, 1.0, 0, 0, 0.0, False, '2026-09-23 00:00:00') /* Linkable Monster Generator (2 min) - respawns the target drudge */
     , (0x7DA55A05, 900021243, 0xDA55001C, 93.386826, 86.979492, 20.011999, 1.0, 0, 0, 0.0, True, '2026-09-23 00:00:00') /* Moving Target Drudge (child of 0x7DA55A04) */;

INSERT INTO `landblock_instance_link` (`parent_GUID`, `child_GUID`, `last_Modified`)
VALUES (0x7DA55A04, 0x7DA55A05, '2026-09-23 00:00:00');
