namespace DaScreenshotStudio.Assets.Fonts;

public readonly struct LftGlyph : IEquatable<LftGlyph>
{
    public const int RecordLength = 11;

    internal LftGlyph(byte advance, byte left, byte top, byte right, byte bottom, int packedSize, int bitmapOffset)
    {
        Advance = advance;
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        PackedSize = packedSize;
        BitmapOffset = bitmapOffset;
    }

    public byte Advance { get; }

    public byte Left { get; }

    public byte Top { get; }

    public byte Right { get; }

    public byte Bottom { get; }

    public int PackedSize { get; }

    public int BitmapOffset { get; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public int RowStride => ((Width + 31) / 32) * 4;

    public bool HasBitmap => PackedSize > 0;

    public bool Equals(LftGlyph other) =>
        Advance == other.Advance && Left == other.Left && Top == other.Top && Right == other.Right
        && Bottom == other.Bottom && PackedSize == other.PackedSize && BitmapOffset == other.BitmapOffset;

    public override bool Equals(object? obj) => obj is LftGlyph other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Advance, Left, Top, Right, Bottom, PackedSize, BitmapOffset);

    public static bool operator ==(LftGlyph left, LftGlyph right) => left.Equals(right);

    public static bool operator !=(LftGlyph left, LftGlyph right) => !left.Equals(right);
}
