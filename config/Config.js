{
  "Server": {
    "WorldName": "ACBuilds",
    "Network": {
      "Host": "0.0.0.0",
      "Port": 9000,
      "MaximumAllowedSessions": 32,
      "DefaultSessionTimeout": 60,
      "MaximumAllowedSessionsPerIPAddress": -1,
      "AllowUnlimitedSessionsFromIPAddresses": []
    },
    "Accounts": {
      "OverrideCharacterPermissions": true,
      "DefaultAccessLevel": 0,
      "AllowAutoAccountCreation": true,
      "PasswordHashWorkFactor": 8,
      "ForceWorkFactorMigration": true
    },
    "DatFilesDirectory": "/ace/Dats",
    "ModsDirectory": "/ace/Mods",
    "ShutdownInterval": "60",
    "ServerPerformanceMonitorAutoStart": false,
    // Tuned for 1-10 players (ACE wiki recommendation)
    "Threading": {
      "WorldThreadCountMultiplier": 0.34,
      "DatabaseThreadCountMultiplier": 0.66,
      "MultiThreadedLandblockGroupPhysicsTicking": false,
      "MultiThreadedLandblockGroupTicking": false
    },
    "ShardPlayerBiotaCacheTime": "31",
    "ShardNonPlayerBiotaCacheTime": "11",
    "WorldDatabasePrecaching": false,
    "LandblockPreloading": true,
    "PreloadedLandblocks": [
      { "Id": "E74EFFFF", "Description": "Hebian-To (Global Events)", "Permaload": true, "IncludeAdjacents": false, "Enabled": true }
    ]
  },
  // Database lives in the same container (MariaDB bound to 127.0.0.1, pre-seeded at image build).
  "MySql": {
    "Authentication": { "Host": "127.0.0.1", "Port": 3306, "Database": "ace_auth",  "Username": "ace", "Password": "ace-local", "EnableDetailedErrors": false, "EnableSensitiveDataLogging": false },
    "Shard":          { "Host": "127.0.0.1", "Port": 3306, "Database": "ace_shard", "Username": "ace", "Password": "ace-local", "EnableDetailedErrors": false, "EnableSensitiveDataLogging": false },
    "World":          { "Host": "127.0.0.1", "Port": 3306, "Database": "ace_world", "Username": "ace", "Password": "ace-local", "EnableDetailedErrors": false, "EnableSensitiveDataLogging": false }
  },
  "Offline": {
    "PurgeDeletedCharacters": false,
    "PurgeDeletedCharactersDays": 30,
    "PurgeOrphanedBiotas": false,
    "PruneDeletedCharactersFromFriendLists": true,
    "PruneDeletedObjectsFromShortcutBars": false,
    "PruneDeletedCharactersFromSquelchLists": false,
    // Pre-seeded at build time; the image is rebuilt when upstream changes, so no runtime patching/downloading.
    "AutoApplyDatabaseUpdates": false,
    "AutoUpdateWorldDatabase": false,
    "AutoApplyWorldCustomizations": true,
    "WorldCustomizationAddedPaths": [],
    "RecurseWorldCustomizationPaths": true
  }
}
