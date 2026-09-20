DELETE FROM `weenie` WHERE `class_Id` = 900021229;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (900021229, 'mossratwarren', 1, '2005-02-09 10:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021229,  66,          1) /* CheckpointStatus */
     , (900021229,  81,          5) /* MaxGeneratedObjects */
     , (900021229,  82,          5) /* InitGeneratedObjects */
     , (900021229,  93,       1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (900021229,   1, True ) /* Stuck */
     , (900021229,  11, True ) /* IgnoreCollisions */
     , (900021229,  18, True ) /* Visibility */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (900021229,  41,      60) /* RegenerationInterval */
     , (900021229,  43,      10) /* GeneratorRadius */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (900021229,   1, 'Moss Rat Warren') /* Name */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (900021229,   1, 0x0200026B) /* Setup */
     , (900021229,   8, 0x06001066) /* Icon */;

INSERT INTO `weenie_properties_generator` (`object_Id`, `probability`, `weenie_Class_Id`, `delay`, `init_Create`, `max_Create`, `when_Create`, `where_Create`, `stack_Size`, `palette_Id`, `shade`, `obj_Cell_Id`, `origin_X`, `origin_Y`, `origin_Z`, `angles_W`, `angles_X`, `angles_Y`, `angles_Z`)
VALUES (900021229, -1, 900021228, 120, 3, 3, 1, 2, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
     (900021229, -1, 900021230, 120, 1, 1, 1, 2, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0),
     (900021229, -1, 900021232, 120, 1, 1, 1, 2, -1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0);

