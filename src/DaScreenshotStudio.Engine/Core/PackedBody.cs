namespace DaScreenshotStudio.Engine.Core;

public readonly struct PackedBody : IEquatable<PackedBody>
{
    public PackedBody(BodyForm form, byte pantsColor)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((byte)form, (byte)0x0F, nameof(form));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pantsColor, (byte)0x0F, nameof(pantsColor));

        Form = form;
        PantsColor = pantsColor;
    }

    public BodyForm Form { get; }

    public byte PantsColor { get; }

    public byte ToByte() => (byte)(((byte)Form << 4) | PantsColor);

    public bool ForcesTranslucent => Form is BodyForm.MaleInvisible or BodyForm.FemaleInvisible;

    public bool IsNonblocking => Form is BodyForm.MaleGhost or BodyForm.FemaleGhost;

    public bool Equals(PackedBody other) => Form == other.Form && PantsColor == other.PantsColor;

    public override bool Equals(object? obj) => obj is PackedBody other && Equals(other);

    public override int GetHashCode() => ToByte();

    public static bool operator ==(PackedBody left, PackedBody right) => left.Equals(right);

    public static bool operator !=(PackedBody left, PackedBody right) => !left.Equals(right);

    public override string ToString() => $"{Form}+{PantsColor}";
}
