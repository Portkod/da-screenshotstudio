using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public readonly struct HumanAppearance : IEquatable<HumanAppearance>
{
    public PackedBody Body { get; init; }

    public ushort ArmsSprite { get; init; }

    public byte BootsSprite { get; init; }

    public ushort ArmorSprite { get; init; }

    public byte ShieldSprite { get; init; }

    public ushort WeaponSprite { get; init; }

    public byte HairColor { get; init; }

    public byte BootsColor { get; init; }

    public byte Accessory1Color { get; init; }

    public ushort Accessory1Sprite { get; init; }

    public byte Accessory2Color { get; init; }

    public ushort Accessory2Sprite { get; init; }

    public byte Accessory3Color { get; init; }

    public ushort Accessory3Sprite { get; init; }

    public byte LightMaskId { get; init; }

    public byte RestPosition { get; init; }

    public ushort OvercoatSprite { get; init; }

    public byte OvercoatColor { get; init; }

    public byte SkinColor { get; init; }

    public bool IsTranslucent { get; init; }

    public byte FaceShape { get; init; }

    public bool Equals(HumanAppearance other) =>
        Body == other.Body &&
        ArmsSprite == other.ArmsSprite &&
        BootsSprite == other.BootsSprite &&
        ArmorSprite == other.ArmorSprite &&
        ShieldSprite == other.ShieldSprite &&
        WeaponSprite == other.WeaponSprite &&
        HairColor == other.HairColor &&
        BootsColor == other.BootsColor &&
        Accessory1Color == other.Accessory1Color &&
        Accessory1Sprite == other.Accessory1Sprite &&
        Accessory2Color == other.Accessory2Color &&
        Accessory2Sprite == other.Accessory2Sprite &&
        Accessory3Color == other.Accessory3Color &&
        Accessory3Sprite == other.Accessory3Sprite &&
        LightMaskId == other.LightMaskId &&
        RestPosition == other.RestPosition &&
        OvercoatSprite == other.OvercoatSprite &&
        OvercoatColor == other.OvercoatColor &&
        SkinColor == other.SkinColor &&
        IsTranslucent == other.IsTranslucent &&
        FaceShape == other.FaceShape;

    public override bool Equals(object? obj) => obj is HumanAppearance other && Equals(other);

    public override int GetHashCode()
    {
        var first = HashCode.Combine(Body, ArmsSprite, BootsSprite, ArmorSprite, ShieldSprite, WeaponSprite);
        var second = HashCode.Combine(
            Accessory1Sprite, Accessory2Sprite, Accessory3Sprite, OvercoatSprite, SkinColor, FaceShape);
        return HashCode.Combine(first, second, HairColor, BootsColor, LightMaskId, RestPosition, IsTranslucent);
    }

    public static bool operator ==(HumanAppearance left, HumanAppearance right) => left.Equals(right);

    public static bool operator !=(HumanAppearance left, HumanAppearance right) => !left.Equals(right);
}
