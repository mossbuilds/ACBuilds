DELETE FROM `landblock_instance` WHERE `guid` IN (0x7DA55A01, 0x7DA55A02);

INSERT INTO `landblock_instance` (`guid`, `weenie_Class_Id`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`, `is_Link_Child`, `last_Modified`)
VALUES (0x7DA55A01, 900021227, 0xDA55001C, 82.072884, 93.409874, 20.004999, 0.290523, 0, 0, 0.956868, False, '2026-09-20 00:00:00') /* Moss the Traveler */;  /* rat warren (900021229) NOT placed here: Shoushi is heavy for headsets. Place it away from town with /createinst 900021229 */
