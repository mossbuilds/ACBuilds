-- RaiseSkeleton feral skeleton: clone of retail 1762 (Skeleton Lord, level 40). Death treasure and create list removed so raised corpses cannot farm loot.
DELETE FROM `weenie` WHERE `class_Id` = 900021241;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (900021241, 'raiseskelferal', 10, '2022-12-04 19:04:52') /* Creature */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021241,   1,         16) /* ItemType - Creature */
     , (900021241,   2,         30) /* CreatureType - Skeleton */
     , (900021241,   6,         -1) /* ItemsCapacity */
     , (900021241,   7,         -1) /* ContainersCapacity */
     , (900021241,  16,          1) /* ItemUseable - No */
     , (900021241,  25,         40) /* Level */
     , (900021241,  27,          0) /* ArmorType - None */
     , (900021241,  40,          1) /* CombatMode - NonCombat */
     , (900021241,  68,          5) /* TargetingTactic - Random, LastDamager */
     , (900021241,  93,       1032) /* PhysicsState - ReportCollisions, Gravity */
     , (900021241, 101,        183) /* AiAllowedCombatStyle - Unarmed, OneHanded, OneHandedAndShield, Bow, Crossbow, ThrownWeapon */
     , (900021241, 133,          2) /* ShowableOnRadar - ShowMovement */
     , (900021241, 140,          1) /* AiOptions - CanOpenDoors */
     , (900021241, 146,       7000) /* XpOverride */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (900021241,   1, True ) /* Stuck */
     , (900021241,   6, True ) /* AiUsesMana */
     , (900021241,  11, False) /* IgnoreCollisions */
     , (900021241,  12, True ) /* ReportCollisions */
     , (900021241,  13, False) /* Ethereal */
     , (900021241,  14, True ) /* GravityStatus */
     , (900021241,  19, True ) /* Attackable */
     , (900021241,  50, True ) /* NeverFailCasting */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (900021241,   1,       5) /* HeartbeatInterval */
     , (900021241,   2,       0) /* HeartbeatTimestamp */
     , (900021241,   3,     0.1) /* HealthRate */
     , (900021241,   4,     0.5) /* StaminaRate */
     , (900021241,   5,       2) /* ManaRate */
     , (900021241,  13,    0.37) /* ArmorModVsSlash */
     , (900021241,  14,    0.16) /* ArmorModVsPierce */
     , (900021241,  15,     0.5) /* ArmorModVsBludgeon */
     , (900021241,  16,    0.05) /* ArmorModVsCold */
     , (900021241,  17,    0.82) /* ArmorModVsFire */
     , (900021241,  18,    0.17) /* ArmorModVsAcid */
     , (900021241,  19,    0.33) /* ArmorModVsElectric */
     , (900021241,  31,      16) /* VisualAwarenessRange */
     , (900021241,  34,       1) /* PowerupTime */
     , (900021241,  36,       1) /* ChargeSpeed */
     , (900021241,  64,    0.58) /* ResistSlash */
     , (900021241,  65,    0.25) /* ResistPierce */
     , (900021241,  66,       1) /* ResistBludgeon */
     , (900021241,  67,     0.9) /* ResistFire */
     , (900021241,  68,     0.3) /* ResistCold */
     , (900021241,  69,    0.42) /* ResistAcid */
     , (900021241,  70,     0.4) /* ResistElectric */
     , (900021241,  71,       1) /* ResistHealthBoost */
     , (900021241,  72,       1) /* ResistStaminaDrain */
     , (900021241,  73,       1) /* ResistStaminaBoost */
     , (900021241,  74,       1) /* ResistManaDrain */
     , (900021241,  75,       1) /* ResistManaBoost */
     , (900021241,  80,       3) /* AiUseMagicDelay */
     , (900021241, 104,      10) /* ObviousRadarRange */
     , (900021241, 125,       1) /* ResistHealthDrain */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (900021241,   1, 'Feral Skeleton') /* Name */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (900021241,   1, 0x02000408) /* Setup */
     , (900021241,   2, 0x09000025) /* MotionTable */
     , (900021241,   3, 0x2000001E) /* SoundTable */
     , (900021241,   4, 0x30000000) /* CombatTable */
     , (900021241,   6, 0x04001DEA) /* PaletteBase */
     , (900021241,   8, 0x060016C4) /* Icon */
     , (900021241,  22, 0x34000025) /* PhysicsEffectTable */
     , (900021241,  32,        189) /* WieldedTreasureType - 
                                   # Set: 1
                                   |   9.00% chance of Battle Axe (301)
                                   |   4.00% chance of Broad Sword (350)
                                   |   4.00% chance of Kaskara (324)
                                   |   4.00% chance of Ken (327)
                                   |   4.00% chance of Long Sword (351)
                                   |   6.00% chance of Morning Star (332)
                                   |   4.00% chance of Scimitar (339)
                                   |   4.00% chance of Shamshir (340)
                                   |   8.00% chance of Ono (336)
                                   |   8.00% chance of Silifi (344)
                                   |   5.00% chance of Tachi (353)
                                   |   5.00% chance of Takuba (354)
                                   |   6.00% chance of 5x to 6x Throwing Axe (304) | StackSizeVariance: 0.1
                                   |   6.00% chance of Nayin (334)
                                   |         with
                                   |            100.00% chance of 14x to 16x Arrow (300) | StackSizeVariance: 0.1
                                   |   6.00% chance of Longbow (306)
                                   |         with
                                   |            100.00% chance of 18x to 20x Arrow (300) | StackSizeVariance: 0.1
                                   |   6.00% chance of Yumi (363)
                                   |         with
                                   |            100.00% chance of 18x to 20x Arrow (300) | StackSizeVariance: 0.1
                                   |  11.00% chance of Heavy Crossbow (311)
                                   |         with
                                   |            100.00% chance of 14x to 16x Quarrel (305) | StackSizeVariance: 0.1
                                   # Set: 2
                                   |  30.00% chance of Large Kite Shield (92)
                                   |  20.00% chance of Kite Shield (91)
                                   |  20.00% chance of Large Round Shield (94)
                                   |  30.00% chance of nothing from this set */;

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (900021241,   1,  65, 0, 0) /* Strength */
     , (900021241,   2,  75, 0, 0) /* Endurance */
     , (900021241,   3, 120, 0, 0) /* Quickness */
     , (900021241,   4, 115, 0, 0) /* Coordination */
     , (900021241,   5, 120, 0, 0) /* Focus */
     , (900021241,   6, 130, 0, 0) /* Self */;

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (900021241,   1,    70, 0, 0, 108) /* MaxHealth */
     , (900021241,   3,    90, 0, 0, 165) /* MaxStamina */
     , (900021241,   5,   100, 0, 0, 230) /* MaxMana */;

INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`)
VALUES (900021241,  6, 0, 3, 0,  50, 0, 0) /* MeleeDefense        Specialized */
     , (900021241,  7, 0, 3, 0, 150, 0, 0) /* MissileDefense      Specialized */
     , (900021241, 14, 0, 3, 0, 110, 0, 0) /* ArcaneLore          Specialized */
     , (900021241, 15, 0, 3, 0, 100, 0, 0) /* MagicDefense        Specialized */
     , (900021241, 20, 0, 2, 0, 120, 0, 0) /* Deception           Trained */
     , (900021241, 31, 0, 3, 0,  85, 0, 0) /* CreatureEnchantment Specialized */
     , (900021241, 33, 0, 3, 0,  85, 0, 0) /* LifeMagic           Specialized */
     , (900021241, 34, 0, 3, 0,  85, 0, 0) /* WarMagic            Specialized */
     , (900021241, 44, 0, 3, 0, 100, 0, 0) /* HeavyWeapons        Specialized */
     , (900021241, 45, 0, 3, 0, 100, 0, 0) /* LightWeapons        Specialized */
     , (900021241, 46, 0, 3, 0,  50, 0, 0) /* FinesseWeapons      Specialized */
     , (900021241, 47, 0, 3, 0, 140, 0, 0) /* MissileWeapons      Specialized */;

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (900021241,  0,  4,  0,    0,   90,   33,   14,   45,    5,   74,   15,   30,    0, 1, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0) /* Head */
     , (900021241,  1,  4,  0,    0,   80,   30,   13,   40,    4,   66,   14,   26,    0, 2, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0) /* Chest */
     , (900021241,  2,  4,  0,    0,   80,   30,   13,   40,    4,   66,   14,   26,    0, 3,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0) /* Abdomen */
     , (900021241,  3,  4,  0,    0,   60,   22,   10,   30,    3,   49,   10,   20,    0, 1, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0) /* UpperArm */
     , (900021241,  4,  4,  0,    0,   50,   19,    8,   25,    3,   41,    9,   17,    0, 2,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0) /* LowerArm */
     , (900021241,  5,  4,  4, 0.75,   60,   22,   10,   30,    3,   49,   10,   20,    0, 2,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0) /* Hand */
     , (900021241,  6,  4,  0,    0,   65,   24,   10,   33,    3,   53,   11,   21,    0, 3,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18) /* UpperLeg */
     , (900021241,  7,  4,  0,    0,   65,   24,   10,   33,    3,   53,   11,   21,    0, 3,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6) /* LowerLeg */
     , (900021241,  8,  4,  5, 0.75,   75,   28,   12,   38,    4,   61,   13,   25,    0, 3,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22) /* Foot */;

INSERT INTO `weenie_properties_spell_book` (`object_Id`, `spell`, `probability`)
VALUES (900021241,    88,  2.105)  /* Force Bolt III */
     , (900021241,    94,  2.105)  /* Whirling Blade III */
     , (900021241,  1340,  2.023)  /* Weakness Other III */
     , (900021241,  1369,  2.023)  /* Frailty Other III */
     , (900021241,  1393,  2.023)  /* Clumsiness Other III */
     , (900021241,  1417,  2.023)  /* Slowness Other III */;

INSERT INTO `weenie_properties_event_filter` (`object_Id`, `event`)
VALUES (900021241,  94) /* ATTACK_NOTIFICATION_EVENT */
     , (900021241, 414) /* PLAYER_DEATH_EVENT */;

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (900021241,  3 /* Death */,      1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id,  0,  88 /* LocalSignal */, 0, 1, NULL, 'ColoCritterKilled', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (900021241,  5 /* HeartBeat */,    0.8, NULL, 0x8000003D /* NonCombat */, 0x41000003 /* Ready */, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id,  0,   5 /* Motion */, 0, 1, 0x41000014 /* Sleeping */, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (900021241,  9 /* Generation */,      1, NULL, NULL, NULL, NULL, NULL, NULL, NULL);

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id,  0,  88 /* LocalSignal */, 0, 1, NULL, 'ColoCritterSpawned', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
