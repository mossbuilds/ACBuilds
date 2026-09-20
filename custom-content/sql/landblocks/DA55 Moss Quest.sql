/* Nothing of ours is placed in Shoushi (0xDA55): it is heavy for standalone headsets. Place the Traveler / rat warren elsewhere with /createinst. */
DELETE FROM `landblock_instance` WHERE `guid` IN (0x7DA55A01, 0x7DA55A02);
