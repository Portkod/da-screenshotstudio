namespace DaScreenshotStudio.Assets.Characters;

public struct HumanAppearance
{
    public const int NoPaletteSelector = -1;

    public const int FirstDyedBodySprite = 7;

    public HumanBody Body;

    public ushort HeadSprite;

    public ushort ArmsSprite;

    public ushort BootsSprite;

    public ushort ArmorSprite;

    public ushort ShieldSprite;

    public ushort WeaponSprite;

    public ushort OvercoatSprite;

    public ushort Accessory1Sprite;

    public ushort Accessory2Sprite;

    public ushort Accessory3Sprite;

    public byte FaceShape;

    public byte HairColor;

    public byte BootsColor;

    public byte Accessory1Color;

    public byte Accessory2Color;

    public byte Accessory3Color;

    public byte OvercoatColor;

    public byte SkinColor;

    public byte ArmorColor;

    public byte LightMaskId;

    public byte RestPosition;

    public bool IsTranslucent;

    public readonly bool HasOvercoat => OvercoatSprite != 0;

    public readonly int GetPartSprite(int category) => category switch
    {
        HumanPartCategory.Shield => ShieldSprite,
        HumanPartCategory.Body => Body.BodySprite,
        HumanPartCategory.Pants => HasOvercoat ? 0 : Body.PantsSprite,
        HumanPartCategory.Boots => BootsSprite,
        HumanPartCategory.Head => HeadSprite,
        HumanPartCategory.Armor => HasOvercoat ? 0 : ArmorSprite,

        HumanPartCategory.InnerClothing => 0,
        HumanPartCategory.Arms => HasOvercoat ? 0 : ArmsSprite,
        HumanPartCategory.Weapon or HumanPartCategory.WeaponAlternate => WeaponSprite,
        HumanPartCategory.Accessory1Front or HumanPartCategory.Accessory1Back => Accessory1Sprite,
        HumanPartCategory.Accessory2Front or HumanPartCategory.Accessory2Back => Accessory2Sprite,
        HumanPartCategory.Accessory3Front or HumanPartCategory.Accessory3Back => Accessory3Sprite,
        HumanPartCategory.HeadAlternate1 or HumanPartCategory.HeadAlternate2 => HeadSprite,
        HumanPartCategory.Overcoat or HumanPartCategory.OvercoatArms => OvercoatSprite,
        HumanPartCategory.Face => FaceShape,
        _ => 0,
    };

    public const int PaletteShiftedBodySprite = 6;

    public readonly int GetPaletteSpriteId(int category, int spriteId) =>
        category == HumanPartCategory.Body
        && Body.BodySprite == PaletteShiftedBodySprite
        && Body.Prefix != 'w'
            ? spriteId - 1
            : spriteId;

    public readonly int GetPaletteSelector(int category) => category switch
    {
        HumanPartCategory.Body => Body.UsesSkinPalette ? SkinColor : NoPaletteSelector,
        HumanPartCategory.Face => SkinColor,
        _ => NoPaletteSelector,
    };

    public readonly bool TryGetDye(int category, out byte dye)
    {
        switch (category)
        {
            case HumanPartCategory.Head:
            case HumanPartCategory.HeadAlternate1:
            case HumanPartCategory.HeadAlternate2:
                dye = HairColor;
                return true;

            case HumanPartCategory.Pants:
                dye = Body.PantsColor;
                return true;

            case HumanPartCategory.Boots:
                dye = BootsColor;
                return true;

            case HumanPartCategory.Body:
                dye = HairColor;
                return Body.BodySprite >= FirstDyedBodySprite;

            case HumanPartCategory.Armor:
                dye = ArmorColor;
                return dye != 0;

            case HumanPartCategory.Accessory1Front:
            case HumanPartCategory.Accessory1Back:
                dye = Accessory1Color;
                return dye != 0;

            case HumanPartCategory.Accessory2Front:
            case HumanPartCategory.Accessory2Back:
                dye = Accessory2Color;
                return dye != 0;

            case HumanPartCategory.Accessory3Front:
            case HumanPartCategory.Accessory3Back:
                dye = Accessory3Color;
                return dye != 0;

            case HumanPartCategory.Overcoat:
            case HumanPartCategory.OvercoatArms:
                dye = OvercoatColor;
                return dye != 0;

            default:
                dye = 0;
                return false;
        }
    }
}
