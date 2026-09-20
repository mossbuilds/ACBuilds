DELETE FROM `quest` WHERE `name` IN ('MossTravStart', 'MossTravRats', 'MossTravP1Done', 'MossTravP2Done', 'MossTravBoss', 'MossTravP3Done');

INSERT INTO `quest` (`name`, `min_Delta`, `max_Solves`, `message`, `last_Modified`)
VALUES ('MossTravStart', 0, 1, 'Moss the Traveler asked you to clear the rats from the warren east of Shoushi.', '2026-09-20 00:00:00')
     , ('MossTravRats', 0, 3, 'Kill three Tunnel Rats in the rat warren east of Shoushi for Moss the Traveler.', '2026-09-20 00:00:00')
     , ('MossTravP1Done', 0, 1, 'You cleared the tunnel rats for Moss the Traveler. Next: bring him a Rat King Tail.', '2026-09-20 00:00:00')
     , ('MossTravP2Done', 0, 1, 'You brought Moss the Traveler the Rat King Tail. Next: kill Old Whiskers.', '2026-09-20 00:00:00')
     , ('MossTravBoss', 0, 1, 'Kill Old Whiskers in the rat warren east of Shoushi.', '2026-09-20 00:00:00')
     , ('MossTravP3Done', 0, 1, 'You finished the Moss the Traveler rat quest.', '2026-09-20 00:00:00');
