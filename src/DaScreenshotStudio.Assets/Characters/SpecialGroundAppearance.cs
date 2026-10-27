namespace DaScreenshotStudio.Assets.Characters;

public static class SpecialGroundAppearance
{
    private const int MaleJesterBodySprite = 4;

    private const int FemaleJesterBodySprite = 3;

    public const int DefaultBodySprite = 6;

    public const int SharedBodySprite = 7;

    public const int MaleBodySprite = 8;

    public const int FemaleBodySprite = 9;

    private const int SharedArmor1 = 318;

    private const int SharedArmor2 = 319;

    private const int MaleArmor = 167;

    private const int FemaleArmor = 171;

    private const int SharedAccessory = 30;

    private const int SharedArmor = 159;

    private const int SharedHead = 102;

    public static bool Substitutes(in HumanAppearance appearance)
    {
        var sprite = appearance.Body.BodySprite;

        return appearance.Body.Prefix == 'w'
            ? sprite is HumanBody.NormalBodySprite or FemaleJesterBodySprite
            : sprite is HumanBody.NormalBodySprite or MaleJesterBodySprite;
    }

    public static HumanAppearance Clone(in HumanAppearance appearance)
    {
        if (!Substitutes(in appearance))
        {
            return appearance;
        }

        var female = appearance.Body.Prefix == 'w';

        var sprite = DefaultBodySprite;

        if (appearance.ArmorSprite is SharedArmor1 or SharedArmor2
            || appearance.ArmorSprite == (female ? FemaleArmor : MaleArmor))
        {
            sprite = female ? FemaleBodySprite : MaleBodySprite;
        }
        else if (appearance.Accessory1Sprite == SharedAccessory
            || appearance.ArmorSprite == SharedArmor
            || appearance.HeadSprite == SharedHead)
        {
            sprite = SharedBodySprite;
        }

        return new HumanAppearance
        {
            Body = HumanBody.OnSpecialGround(in appearance.Body, sprite),
            HairColor = appearance.HairColor,
            BootsColor = appearance.BootsColor,
        };
    }
}
