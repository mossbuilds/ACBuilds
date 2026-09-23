/* Moving Target Drudge (Tom 2026-09-23): a Drudge Skulker (5595) clone that walks a loop around Shoushi and only fights back after being attacked. */
DELETE FROM `weenie` WHERE `class_Id` = 900021243;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (900021243, 'movingtargetdrudge', 10, '2005-02-09 10:00:00') /* Creature */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021243,   1,         16) /* ItemType - Creature */
     , (900021243,   2,          3) /* CreatureType - Drudge */
     , (900021243,   3,         48) /* PaletteTemplate - SnowyWhite */
     , (900021243,   6,         -1) /* ItemsCapacity */
     , (900021243,   7,         -1) /* ContainersCapacity */
     , (900021243,  16,          1) /* ItemUseable - No */
     , (900021243,  25,          4) /* Level */
     , (900021243,  27,          0) /* ArmorType - None */
     , (900021243,  40,          2) /* CombatMode - Melee */
     , (900021243,  68,          1) /* TargetingTactic - Random */
     , (900021243,  93,    2098200) /* PhysicsState - ReportCollisions, IgnoreCollisions, Gravity, ReportCollisionsAsEnvironment */
     , (900021243, 101,        131) /* AiAllowedCombatStyle - Unarmed, OneHanded, ThrownWeapon */
     , (900021243, 133,          4) /* ShowableOnRadar - ShowAlways */
     , (900021243, 146,         45) /* XpOverride */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (900021243,   1, True ) /* Stuck */
     , (900021243,  12, True ) /* ReportCollisions */
     , (900021243,  13, False) /* Ethereal */
     , (900021243,  41, True ) /* ReportCollisionsAsEnvironment */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (900021243,   1,      60) /* HeartbeatInterval - 60 s, so the 48 s patrol never re-triggers mid-walk */
     , (900021243,   2,       0) /* HeartbeatTimestamp */
     , (900021243,   3,   0.067) /* HealthRate */
     , (900021243,   4,       5) /* StaminaRate */
     , (900021243,   5,       1) /* ManaRate */
     , (900021243,  12,       1) /* Shade */
     , (900021243,  13,     0.9) /* ArmorModVsSlash */
     , (900021243,  14,       1) /* ArmorModVsPierce */
     , (900021243,  15,     1.1) /* ArmorModVsBludgeon */
     , (900021243,  16,     0.6) /* ArmorModVsCold */
     , (900021243,  17,     0.6) /* ArmorModVsFire */
     , (900021243,  18,       1) /* ArmorModVsAcid */
     , (900021243,  19,    0.36) /* ArmorModVsElectric */
     , (900021243,  31,      10) /* VisualAwarenessRange */
     , (900021243,  34,       1) /* PowerupTime */
     , (900021243,  36,       1) /* ChargeSpeed */
     , (900021243,  39,    0.95) /* DefaultScale */
     , (900021243,  64,    0.86) /* ResistSlash */
     , (900021243,  65,    0.75) /* ResistPierce */
     , (900021243,  66,    0.66) /* ResistBludgeon */
     , (900021243,  67,    1.42) /* ResistFire */
     , (900021243,  68,    1.42) /* ResistCold */
     , (900021243,  69,    0.75) /* ResistAcid */
     , (900021243,  70,    1.42) /* ResistElectric */
     , (900021243,  71,       1) /* ResistHealthBoost */
     , (900021243,  72,       1) /* ResistStaminaDrain */
     , (900021243,  73,       1) /* ResistStaminaBoost */
     , (900021243,  74,       1) /* ResistManaDrain */
     , (900021243,  75,       1) /* ResistManaBoost */
     , (900021243, 104,      10) /* ObviousRadarRange */
     , (900021243, 125,       1) /* ResistHealthDrain */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (900021243,   1, 'Moving Target Drudge') /* Name */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (900021243,   1, 0x020007DD) /* Setup */
     , (900021243,   2, 0x09000008) /* MotionTable */
     , (900021243,   3, 0x20000007) /* SoundTable */
     , (900021243,   4, 0x30000004) /* CombatTable */
     , (900021243,   6, 0x04000F6C) /* PaletteBase */
     , (900021243,   7, 0x10000206) /* ClothingBase */
     , (900021243,   8, 0x06001035) /* Icon */
     , (900021243,  22, 0x3400001A) /* PhysicsEffectTable */
     , (900021243,  35,        453) /* DeathTreasureType - Loot Tier: 1 */;

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (900021243,   1,  20, 0, 0) /* Strength */
     , (900021243,   2,  30, 0, 0) /* Endurance */
     , (900021243,   3,  30, 0, 0) /* Quickness */
     , (900021243,   4,  25, 0, 0) /* Coordination */
     , (900021243,   5,  25, 0, 0) /* Focus */
     , (900021243,   6,  15, 0, 0) /* Self */;

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (900021243,   1,     5, 0, 0, 20) /* MaxHealth */
     , (900021243,   3,    50, 0, 0, 80) /* MaxStamina */
     , (900021243,   5,     0, 0, 0, 15) /* MaxMana */;

INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`)
VALUES (900021243,  1, 0, 3, 0,   5, 0, 432.91977126602785) /* Axe                 Specialized */
     , (900021243,  4, 0, 3, 0,   5, 0, 432.91977126602785) /* Dagger              Specialized */
     , (900021243,  5, 0, 3, 0,   5, 0, 432.91977126602785) /* Mace                Specialized */
     , (900021243,  6, 0, 3, 0,   5, 0, 432.91977126602785) /* MeleeDefense        Specialized */
     , (900021243,  7, 0, 3, 0,  15, 0, 432.91977126602785) /* MissileDefense      Specialized */
     , (900021243,  9, 0, 3, 0,   5, 0, 432.91977126602785) /* Spear               Specialized */
     , (900021243, 10, 0, 3, 0,   5, 0, 432.91977126602785) /* Staff               Specialized */
     , (900021243, 11, 0, 3, 0,   5, 0, 432.91977126602785) /* Sword               Specialized */
     , (900021243, 13, 0, 3, 0,   5, 0, 432.91977126602785) /* UnarmedCombat       Specialized */
     , (900021243, 15, 0, 3, 0,   5, 0, 432.91977126602785) /* MagicDefense        Specialized */
     , (900021243, 24, 0, 2, 0,  40, 0, 432.91977126602785) /* Run                 Trained */;

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (900021243,  0,  4,  0,    0,    3,    3,    3,    3,    2,    2,    3,    1,    0, 1, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0) /* Head */
     , (900021243,  1,  4,  0,    0,    7,    6,    7,    8,    4,    4,    7,    3,    0, 2, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0) /* Chest */
     , (900021243,  2,  4,  0,    0,    7,    6,    7,    8,    4,    4,    7,    3,    0, 3,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0) /* Abdomen */
     , (900021243,  3,  4,  0,    0,    5,    5,    5,    6,    3,    3,    5,    2,    0, 1, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0) /* UpperArm */
     , (900021243,  4,  4,  0,    0,    7,    6,    7,    8,    4,    4,    7,    3,    0, 2,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0) /* LowerArm */
     , (900021243,  5,  4,  2, 0.75,    5,    5,    5,    6,    3,    3,    5,    2,    0, 2,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0) /* Hand */
     , (900021243,  6,  4,  0,    0,    5,    5,    5,    6,    3,    3,    5,    2,    0, 3,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18) /* UpperLeg */
     , (900021243,  7,  4,  0,    0,    5,    5,    5,    6,    3,    3,    5,    2,    0, 3,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6) /* LowerLeg */
     , (900021243,  8,  4,  3, 0.75,    5,    5,    5,    6,    3,    3,    5,    2,    0, 3,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22) /* Foot */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021243,  67,         64) /* Tolerance - Retaliate: only fights back after being attacked */;

INSERT INTO `weenie_properties_emote` (`object_Id`, `category`, `probability`, `weenie_Class_Id`, `style`, `substyle`, `quest`, `vendor_Type`, `min_Health`, `max_Health`)
VALUES (900021243, 5, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL) /* HeartBeat: walk a loop around the Shoushi market square */;

SET @parent_id = LAST_INSERT_ID();

INSERT INTO `weenie_properties_emote_action` (`emote_Id`, `order`, `type`, `delay`, `extent`, `motion`, `message`, `test_String`, `min`, `max`, `min_64`, `max_64`, `min_Dbl`, `max_Dbl`, `stat`, `display`, `amount`, `amount_64`, `hero_X_P_64`, `percent`, `spell_Id`, `wealth_Rating`, `treasure_Class`, `treasure_Type`, `p_Script`, `sound`, `destination_Type`, `weenie_Class_Id`, `stack_Size`, `palette`, `shade`, `try_To_Bond`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (@parent_id, 0, 87, 0, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3663003676, 93.386826, 86.979492, 20.011999, 0.647185, 0, 0, 0.762333),
     (@parent_id, 1, 87, 16, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3663003677, 84.800003, 99.0, 20.0, 1.0, 0, 0, 0.0),
     (@parent_id, 2, 87, 16, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3663003676, 84.734375, 94.191406, 20.0, -0.059209, 0, 0, 0.998246),
     (@parent_id, 3, 87, 16, 1, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3663003676, 96.363045, 86.627693, 20.004999, -0.967637, 0, 0, -0.252347);
