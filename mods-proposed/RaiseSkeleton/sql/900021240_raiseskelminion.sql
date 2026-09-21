-- RaiseSkeleton minion: clone of retail CombatPet weenie 48943 (skeleton summon). Lifespan row removed: the mod owns the lifetime.
DELETE FROM `weenie` WHERE `class_Id` = 900021240;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (900021240, 'raiseskelminion', 71, '2022-12-04 19:04:52') /* CombatPet */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021240,   1,         16) /* ItemType - Creature */
     , (900021240,   2,         30) /* CreatureType - Skeleton */
     , (900021240,   3,          2) /* PaletteTemplate - Blue */
     , (900021240,   6,         -1) /* ItemsCapacity */
     , (900021240,   7,         -1) /* ContainersCapacity */
     , (900021240,  16,          1) /* ItemUseable - No */
     , (900021240,  25,         50) /* Level */
     , (900021240,  40,          1) /* CombatMode - NonCombat */
     , (900021240,  68,         64) /* TargetingTactic - Nearest */
     , (900021240,  93,       1036) /* PhysicsState - Ethereal, ReportCollisions, Gravity */
     , (900021240, 133,          1) /* ShowableOnRadar - ShowNever */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (900021240,   1, True ) /* Stuck */
     , (900021240,  12, True ) /* ReportCollisions */
     , (900021240,  13, True ) /* Ethereal */
     , (900021240,  14, True ) /* GravityStatus */
     , (900021240,  19, True ) /* Attackable */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (900021240,  12,     0.5) /* Shade */
     , (900021240,  31,      25) /* VisualAwarenessRange */
     , (900021240,  77,       1) /* PhysicsScriptIntensity */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (900021240,   1, 'Skeletal Minion') /* Name */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (900021240,   1, 0x02001B96) /* Setup */
     , (900021240,   2, 0x09000001) /* MotionTable */
     , (900021240,   3, 0x2000001E) /* SoundTable */
     , (900021240,   4, 0x30000000) /* CombatTable */
     , (900021240,   7, 0x10000827) /* ClothingBase */
     , (900021240,   8, 0x060016C4) /* Icon */
     , (900021240,  22, 0x34000025) /* PhysicsEffectTable */;

INSERT INTO `weenie_properties_attribute` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`)
VALUES (900021240,   1, 130, 0, 0) /* Strength */
     , (900021240,   2, 160, 0, 0) /* Endurance */
     , (900021240,   3,  80, 0, 0) /* Quickness */
     , (900021240,   4,  90, 0, 0) /* Coordination */
     , (900021240,   5, 100, 0, 0) /* Focus */
     , (900021240,   6, 100, 0, 0) /* Self */;

INSERT INTO `weenie_properties_attribute_2nd` (`object_Id`, `type`, `init_Level`, `level_From_C_P`, `c_P_Spent`, `current_Level`)
VALUES (900021240,   1,   350, 0, 0, 430) /* MaxHealth */
     , (900021240,   3,   450, 0, 0, 610) /* MaxStamina */
     , (900021240,   5,   300, 0, 0, 400) /* MaxMana */;

INSERT INTO `weenie_properties_skill` (`object_Id`, `type`, `level_From_P_P`, `s_a_c`, `p_p`, `init_Level`, `resistance_At_Last_Check`, `last_Used_Time`)
VALUES (900021240,  6, 0, 3, 0, 310, 0, 313.36962890625) /* MeleeDefense        Specialized */
     , (900021240,  7, 0, 3, 0, 310, 0, 313.36962890625) /* MissileDefense      Specialized */
     , (900021240, 15, 0, 3, 0, 310, 0, 313.36962890625) /* MagicDefense        Specialized */
     , (900021240, 20, 0, 3, 0, 310, 0, 313.36962890625) /* Deception           Specialized */
     , (900021240, 45, 0, 3, 0, 310, 0, 313.36962890625) /* LightWeapons        Specialized */
     , (900021240, 51, 0, 3, 0, 310, 0, 313.36962890625) /* SneakAttack         Specialized */;

INSERT INTO `weenie_properties_body_part` (`object_Id`, `key`, `d_Type`, `d_Val`, `d_Var`, `base_Armor`, `armor_Vs_Slash`, `armor_Vs_Pierce`, `armor_Vs_Bludgeon`, `armor_Vs_Cold`, `armor_Vs_Fire`, `armor_Vs_Acid`, `armor_Vs_Electric`, `armor_Vs_Nether`, `b_h`, `h_l_f`, `m_l_f`, `l_l_f`, `h_r_f`, `m_r_f`, `l_r_f`, `h_l_b`, `m_l_b`, `l_l_b`, `h_r_b`, `m_r_b`, `l_r_b`)
VALUES (900021240,  0, 16, 81, 0.75,  310,  310,  310,  310,  310,  310,  310,  310,    0, 1, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0, 0.33,    0,    0) /* Head */
     , (900021240,  1, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 2, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0, 0.44, 0.17,    0) /* Chest */
     , (900021240,  2, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 3,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0,    0, 0.17,    0) /* Abdomen */
     , (900021240,  3, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 1, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0, 0.23, 0.03,    0) /* UpperArm */
     , (900021240,  4, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 2,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0,    0,  0.3,    0) /* LowerArm */
     , (900021240,  5, 16, 81, 0.75,  310,  310,  310,  310,  310,  310,  310,  310,    0, 2,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0,    0,  0.2,    0) /* Hand */
     , (900021240,  6, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 3,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18,    0, 0.13, 0.18) /* UpperLeg */
     , (900021240,  7, 16,  0,    0,  310,  310,  310,  310,  310,  310,  310,  310,    0, 3,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6,    0,    0,  0.6) /* LowerLeg */
     , (900021240,  8, 16, 81, 0.75,  310,  310,  310,  310,  310,  310,  310,  310,    0, 3,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22,    0,    0, 0.22) /* Foot */;
