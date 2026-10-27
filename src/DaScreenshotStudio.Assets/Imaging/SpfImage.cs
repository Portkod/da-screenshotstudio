using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public readonly struct SpfFrame : IEquatable<SpfFrame>
{
    public const int RecordLength = 0x20;

    public const uint AnchorFlag = 1;

    internal SpfFrame(
        int left,
        int top,
        int right,
        int bottom,
        int anchorX,
        int anchorY,
        uint flags,
        int dataOffset,
        int pitch,
        uint unknown18,
        int alternateOffset)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        AnchorX = anchorX;
        AnchorY = anchorY;
        Flags = flags;
        DataOffset = dataOffset;
        Pitch = pitch;
        Unknown18 = unknown18;
        AlternateOffset = alternateOffset;
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }

    public int AnchorX { get; }

    public int AnchorY { get; }

    public uint Flags { get; }

    public int DataOffset { get; }

    public int Pitch { get; }

    public uint Unknown18 { get; }

    public int AlternateOffset { get; }

    public bool HasAnchor => (Flags & AnchorFlag) != 0;

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Equals(SpfFrame other) =>
        Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom
        && AnchorX == other.AnchorX && AnchorY == other.AnchorY && Flags == other.Flags
        && DataOffset == other.DataOffset && Pitch == other.Pitch && Unknown18 == other.Unknown18
        && AlternateOffset == other.AlternateOffset;

    public override bool Equals(object? obj) => obj is SpfFrame other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(HashCode.Combine(Left, Top, Right, Bottom), AnchorX, AnchorY, Flags, DataOffset, Pitch, AlternateOffset);

    public static bool operator ==(SpfFrame left, SpfFrame right) => left.Equals(right);

    public static bool operator !=(SpfFrame left, SpfFrame right) => !left.Equals(right);
}

public sealed class SpfImage
{
    public const int PrefixLength = 0x0C;

    public const int PaletteBlockLength = 0x400;

    public const int PaletteColorCount = 256;

    public const byte IndexedPixelMode = 0;

    public const byte DirectPixelMode = 2;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly SpfFrame[] _frames;
    private readonly ushort[]? _rgb565;
    private readonly ushort[]? _rgb555;
    private readonly int _blobStart;

    private SpfImage(
        ReadOnlyMemory<byte> data,
        byte pixelMode,
        byte paletteMode,
        ushort[]? rgb565,
        ushort[]? rgb555,
        SpfFrame[] frames,
        int blobStart,
        int blobLength)
    {
        _data = data;
        _frames = frames;
        _rgb565 = rgb565;
        _rgb555 = rgb555;
        _blobStart = blobStart;
        PixelMode = pixelMode;
        PaletteMode = paletteMode;
        BlobLength = blobLength;
    }

    public byte PixelMode { get; }

    public byte PaletteMode { get; }

    public int FrameCount => _frames.Length;

    public SpfFrame this[int frameIndex] => _frames[frameIndex];

    public int BlobLength { get; }

    public ReadOnlySpan<ushort> Rgb565Palette => _rgb565;

    public ReadOnlySpan<ushort> Rgb555Palette => _rgb555;

    public static SpfImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < PrefixLength + 4)
        {
            throw new InvalidDataException($"An SPF needs at least {PrefixLength + 4} bytes; got {bytes.Length}.");
        }

        var pixelMode = bytes[8];
        var paletteMode = bytes[9];

        var position = PrefixLength;
        ushort[]? rgb565 = null;
        ushort[]? rgb555 = null;

        if (pixelMode == IndexedPixelMode && paletteMode == IndexedPixelMode)
        {
            if (bytes.Length < position + PaletteBlockLength + 4)
            {
                throw new InvalidDataException("An indexed SPF does not hold its 0x400 byte palette block.");
            }

            rgb565 = ReadPalette(bytes.Slice(position, PaletteColorCount * 2));
            rgb555 = ReadPalette(bytes.Slice(position + (PaletteColorCount * 2), PaletteColorCount * 2));
            position += PaletteBlockLength;
        }

        var frameCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
        position += 4;

        var available = (bytes.Length - position - 4) / SpfFrame.RecordLength;
        if (frameCount > (uint)Math.Max(available, 0))
        {
            throw new InvalidDataException($"An SPF declares {frameCount} frames, which does not fit its {bytes.Length} bytes.");
        }

        var frames = new SpfFrame[frameCount];
        for (var i = 0; i < frames.Length; i++)
        {
            var record = bytes.Slice(position, SpfFrame.RecordLength);
            frames[i] = new SpfFrame(
                BinaryPrimitives.ReadUInt16LittleEndian(record),
                BinaryPrimitives.ReadUInt16LittleEndian(record[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[6..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[10..]),
                BinaryPrimitives.ReadUInt32LittleEndian(record[12..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[16..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[20..]),
                BinaryPrimitives.ReadUInt32LittleEndian(record[24..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[28..]));

            position += SpfFrame.RecordLength;
        }

        var declaredBlob = BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
        position += 4;

        var blobLength = (int)Math.Min(declaredBlob, (uint)(bytes.Length - position));

        return new SpfImage(data, pixelMode, paletteMode, rgb565, rgb555, frames, position, blobLength);
    }

    public ReadOnlySpan<byte> GetPixels(int frameIndex, bool alternateFormat = false)
    {
        var frame = _frames[frameIndex];
        if (frame.IsEmpty || frame.Pitch <= 0)
        {
            return default;
        }

        var offset = frame.DataOffset;
        if (alternateFormat && PixelMode == DirectPixelMode)
        {
            offset += frame.AlternateOffset;
        }

        var length = frame.Pitch * frame.Height;
        if (offset < 0 || length < 0 || (long)offset + length > BlobLength)
        {
            return default;
        }

        return _data.Span.Slice(_blobStart + offset, length);
    }

    private static ushort[] ReadPalette(ReadOnlySpan<byte> block)
    {
        var colors = new ushort[PaletteColorCount];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = BinaryPrimitives.ReadUInt16LittleEndian(block[(i * 2)..]);
        }

        return colors;
    }
}
