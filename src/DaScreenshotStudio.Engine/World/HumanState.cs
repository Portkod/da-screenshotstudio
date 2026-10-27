using DaScreenshotStudio.Assets.Characters;

namespace DaScreenshotStudio.Engine.World;

public readonly struct HumanState : IEquatable<HumanState>
{
    public readonly HumanAppearance Appearance;

    public readonly bool IsHiddenPlaceholder;

    public HumanState(in HumanAppearance appearance, bool isHiddenPlaceholder = false)
    {
        Appearance = appearance;
        IsHiddenPlaceholder = isHiddenPlaceholder;
    }

    public bool IsEmpty => Appearance.Body.Form == HumanBodyForm.None && Appearance.HeadSprite == 0;

    public bool HasNoBody => IsHiddenPlaceholder || Appearance.Body.BodySprite == 0;

    public bool Equals(HumanState other) => Matches(in other);

    public bool Matches(in HumanState other)
    {
        ref readonly var a = ref Appearance;
        ref readonly var b = ref other.Appearance;

        return IsHiddenPlaceholder == other.IsHiddenPlaceholder
            && a.Body == b.Body
            && a.HeadSprite == b.HeadSprite
            && a.ArmsSprite == b.ArmsSprite
            && a.BootsSprite == b.BootsSprite
            && a.ArmorSprite == b.ArmorSprite
            && a.ShieldSprite == b.ShieldSprite
            && a.WeaponSprite == b.WeaponSprite
            && a.OvercoatSprite == b.OvercoatSprite
            && a.Accessory1Sprite == b.Accessory1Sprite
            && a.Accessory2Sprite == b.Accessory2Sprite
            && a.Accessory3Sprite == b.Accessory3Sprite
            && a.FaceShape == b.FaceShape
            && a.HairColor == b.HairColor
            && a.BootsColor == b.BootsColor
            && a.Accessory1Color == b.Accessory1Color
            && a.Accessory2Color == b.Accessory2Color
            && a.Accessory3Color == b.Accessory3Color
            && a.OvercoatColor == b.OvercoatColor
            && a.SkinColor == b.SkinColor
            && a.ArmorColor == b.ArmorColor
            && a.RestPosition == b.RestPosition
            && a.IsTranslucent == b.IsTranslucent;
    }

    public override bool Equals(object? obj) => obj is HumanState other && Equals(other);

    public override int GetHashCode()
    {
        var first = HashCode.Combine(
            Appearance.Body,
            Appearance.HeadSprite,
            Appearance.ArmsSprite,
            Appearance.BootsSprite,
            Appearance.ArmorSprite,
            Appearance.ShieldSprite,
            Appearance.WeaponSprite,
            Appearance.OvercoatSprite);

        var second = HashCode.Combine(
            Appearance.Accessory1Sprite,
            Appearance.Accessory2Sprite,
            Appearance.Accessory3Sprite,
            Appearance.FaceShape,
            Appearance.HairColor,
            Appearance.BootsColor,
            Appearance.SkinColor,
            Appearance.OvercoatColor);

        var third = HashCode.Combine(
            first,
            second,
            Appearance.Accessory1Color,
            Appearance.Accessory2Color,
            Appearance.Accessory3Color,
            Appearance.ArmorColor,
            Appearance.RestPosition,
            Appearance.IsTranslucent);

        return HashCode.Combine(third, IsHiddenPlaceholder);
    }

    public static bool operator ==(HumanState left, HumanState right) => left.Equals(right);

    public static bool operator !=(HumanState left, HumanState right) => !left.Equals(right);
}
