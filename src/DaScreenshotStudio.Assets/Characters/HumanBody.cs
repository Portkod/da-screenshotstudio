namespace DaScreenshotStudio.Assets.Characters;

public enum HumanBodyForm : byte
{
    None = 0x0,
    Male = 0x1,
    Female = 0x2,
    MaleGhost = 0x3,
    FemaleGhost = 0x4,
    MaleInvisible = 0x5,
    FemaleInvisible = 0x6,
    MaleJester = 0x7,
    MaleHead = 0x8,
    FemaleHead = 0x9,
    MaleBlank = 0xA,
    FemaleBlank = 0xB,
}

public readonly struct HumanBody : IEquatable<HumanBody>
{
    public const int NormalBodySprite = 1;

    public const int HeadOnlyBodySprite = 5;

    private HumanBody(HumanBodyForm form, char prefix, int bodySprite, byte pantsColor, bool nonblocking, bool translucent)
    {
        Form = form;
        Prefix = prefix;
        BodySprite = bodySprite;
        PantsColor = pantsColor;
        IsNonblocking = nonblocking;
        ForcesTranslucent = translucent;
    }

    public HumanBodyForm Form { get; }

    public char Prefix { get; }

    public int BodySprite { get; }

    public byte PantsColor { get; }

    public int PantsSprite => PantsColor != 0 ? 1 : 0;

    public bool IsNonblocking { get; }

    public bool ForcesTranslucent { get; }

    public bool UsesBodyMotionFamily => BodySprite is NormalBodySprite or HeadOnlyBodySprite;

    public bool UsesSkinPalette => UsesBodyMotionFamily;

    public static HumanBody OnSpecialGround(in HumanBody source, int bodySprite) =>
        new(source.Form, source.Prefix, bodySprite, 0, false, false);

    public static HumanBody FromPacked(byte packedBody)
    {
        var selector = (byte)(packedBody >> 4);
        var pantsColor = (byte)(packedBody & 0x0F);

        return selector switch
        {
            0x0 => new HumanBody(HumanBodyForm.None, 'm', 0, pantsColor, false, false),
            0x1 => new HumanBody(HumanBodyForm.Male, 'm', 1, pantsColor, false, false),
            0x2 => new HumanBody(HumanBodyForm.Female, 'w', 1, pantsColor, false, false),
            0x3 => new HumanBody(HumanBodyForm.MaleGhost, 'm', 2, pantsColor, true, false),
            0x4 => new HumanBody(HumanBodyForm.FemaleGhost, 'w', 2, pantsColor, true, false),
            0x5 => new HumanBody(HumanBodyForm.MaleInvisible, 'm', 1, pantsColor, false, true),
            0x6 => new HumanBody(HumanBodyForm.FemaleInvisible, 'w', 1, pantsColor, false, true),
            0x7 => new HumanBody(HumanBodyForm.MaleJester, 'm', 4, pantsColor, false, false),
            0x8 => new HumanBody(HumanBodyForm.MaleHead, 'm', 5, pantsColor, false, false),
            0x9 => new HumanBody(HumanBodyForm.FemaleHead, 'w', 5, pantsColor, false, false),

            _ => new HumanBody(
                selector == 0x0A ? HumanBodyForm.MaleBlank : HumanBodyForm.FemaleBlank,
                'm',
                selector,
                pantsColor,
                false,
                false),
        };
    }

    public bool Equals(HumanBody other) =>
        Form == other.Form && Prefix == other.Prefix && BodySprite == other.BodySprite
        && PantsColor == other.PantsColor && IsNonblocking == other.IsNonblocking
        && ForcesTranslucent == other.ForcesTranslucent;

    public override bool Equals(object? obj) => obj is HumanBody other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Form, Prefix, BodySprite, PantsColor, IsNonblocking, ForcesTranslucent);

    public static bool operator ==(HumanBody left, HumanBody right) => left.Equals(right);

    public static bool operator !=(HumanBody left, HumanBody right) => !left.Equals(right);
}
