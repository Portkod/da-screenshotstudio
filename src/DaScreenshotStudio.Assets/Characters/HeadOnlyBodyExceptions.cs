namespace DaScreenshotStudio.Assets.Characters;

public static class HeadOnlyBodyExceptions
{
    public const int OvercoatMotion = 4;

    public static bool ForcesWeaponFrameZero(int bodySprite, int category, int motion) =>
        bodySprite == HumanBody.HeadOnlyBodySprite
        && category == HumanPartCategory.Weapon
        && motion == 0;

    public static bool RedirectsOvercoatMotion(int bodySprite, int category, int motion, int weaponSprite) =>
        weaponSprite == 0
        && bodySprite == HumanBody.HeadOnlyBodySprite
        && category == HumanPartCategory.Overcoat
        && motion == 0;
}
