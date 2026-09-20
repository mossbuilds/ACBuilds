DELETE FROM `weenie` WHERE `class_Id` = 900021231;

INSERT INTO `weenie` (`class_Id`, `class_Name`, `type`, `last_Modified`)
VALUES (900021231, 'mossrattail', 1, '2005-02-09 10:00:00') /* Generic */;

INSERT INTO `weenie_properties_int` (`object_Id`, `type`, `value`)
VALUES (900021231,   1,        128) /* ItemType - Misc */
     , (900021231,   3,          8) /* PaletteTemplate - Green */
     , (900021231,   5,         30) /* EncumbranceVal */
     , (900021231,   8,         10) /* Mass */
     , (900021231,   9,          0) /* ValidLocations - None */
     , (900021231,  16,          1) /* ItemUseable - No */
     , (900021231,  19,          2) /* Value */
     , (900021231,  93,       1044) /* PhysicsState - Ethereal, IgnoreCollisions, Gravity */;

INSERT INTO `weenie_properties_bool` (`object_Id`, `type`, `value`)
VALUES (900021231,  22, True ) /* Inscribable */
     , (900021231,  23, True ) /* DestroyOnSell */;

INSERT INTO `weenie_properties_float` (`object_Id`, `type`, `value`)
VALUES (900021231,  39,     0.4) /* DefaultScale */;

INSERT INTO `weenie_properties_string` (`object_Id`, `type`, `value`)
VALUES (900021231,   1, 'Rat King Tail') /* Name */
     , (900021231,  15, 'The tail of a very large tunnel rat. Moss the Traveler wants it.') /* ShortDesc */;

INSERT INTO `weenie_properties_d_i_d` (`object_Id`, `type`, `value`)
VALUES (900021231,   1, 0x02000181) /* Setup */
     , (900021231,   3, 0x20000014) /* SoundTable */
     , (900021231,   6, 0x04000BEF) /* PaletteBase */
     , (900021231,   7, 0x10000178) /* ClothingBase */
     , (900021231,   8, 0x06001E8E) /* Icon */
     , (900021231,  22, 0x3400002B) /* PhysicsEffectTable */;

