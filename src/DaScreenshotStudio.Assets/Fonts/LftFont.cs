using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Fonts;

public readonly struct LftTextMetrics : IEquatable<LftTextMetrics>
{
    internal LftTextMetrics(int advanceWidth, int inkLeft, int inkTop, int inkRight, int inkBottom)
    {
        AdvanceWidth = advanceWidth;
        InkLeft = inkLeft;
        InkTop = inkTop;
        InkRight = inkRight;
        InkBottom = inkBottom;
    }

    public int AdvanceWidth { get; }

    public int InkLeft { get; }

    public int InkTop { get; }

    public int InkRight { get; }

    public int InkBottom { get; }

    public bool Equals(LftTextMetrics other) =>
        AdvanceWidth == other.AdvanceWidth && InkLeft == other.InkLeft && InkTop == other.InkTop
        && InkRight == other.InkRight && InkBottom == other.InkBottom;

    public override bool Equals(object? obj) => obj is LftTextMetrics other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(AdvanceWidth, InkLeft, InkTop, InkRight, InkBottom);

    public static bool operator ==(LftTextMetrics left, LftTextMetrics right) => left.Equals(right);

    public static bool operator !=(LftTextMetrics left, LftTextMetrics right) => !left.Equals(right);
}

public sealed class LftFont
{
    public const int GlyphCount = 65535;

    public const int HeaderLength = 4;

    public const int BitmapBlockStart = HeaderLength + (GlyphCount * LftGlyph.RecordLength);

    private readonly ReadOnlyMemory<byte> _data;
    private readonly LftGlyph[] _glyphs;

    private LftFont(ReadOnlyMemory<byte> data, int nominalWidth, int nominalHeight, LftGlyph[] glyphs)
    {
        _data = data;
        _glyphs = glyphs;
        NominalWidth = nominalWidth;
        NominalHeight = nominalHeight;
    }

    public int NominalWidth { get; }

    public int NominalHeight { get; }

    public LftGlyph this[int key] => _glyphs[key];

    public static LftFont Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < BitmapBlockStart)
        {
            throw new InvalidDataException(
                $"An LFT needs {BitmapBlockStart} bytes for its header and table; got {bytes.Length}.");
        }

        var nominalWidth = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        var nominalHeight = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);

        var block = bytes.Length - BitmapBlockStart;
        var glyphs = new LftGlyph[GlyphCount];

        for (var key = 0; key < GlyphCount; key++)
        {
            var record = bytes.Slice(HeaderLength + (key * LftGlyph.RecordLength), LftGlyph.RecordLength);
            var packedSize = (int)BinaryPrimitives.ReadUInt16LittleEndian(record[5..]);
            var bitmapOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(record[7..]);

            if (bitmapOffset < 0 || packedSize < 0 || (long)bitmapOffset + packedSize > block)
            {
                packedSize = 0;
                bitmapOffset = 0;
            }

            glyphs[key] = new LftGlyph(record[0], record[1], record[2], record[3], record[4], packedSize, bitmapOffset);
        }

        return new LftFont(data, nominalWidth, nominalHeight, glyphs);
    }

    public static bool ContainsKey(int key) => (uint)key < GlyphCount;

    public bool IsDrawn(int key) => GetAdvance(key) != 0;

    public int GetAdvance(int key)
    {
        if (key is 0x08 or 0x09 or 0x0A or 0x0D)
        {
            return 0;
        }

        if (!ContainsKey(key))
        {
            return 0;
        }

        var glyph = _glyphs[key];
        return key == 0x20 && !glyph.HasBitmap ? NominalWidth / 2 : glyph.Advance;
    }

    public LftTextMetrics Measure(scoped ReadOnlySpan<byte> text, LftCodePage codePage)
    {
        var penX = 0;
        var inkLeft = int.MaxValue;
        var inkTop = int.MaxValue;
        var inkRight = int.MinValue;
        var inkBottom = int.MinValue;

        var reader = new LftKeyReader(text, codePage);
        while (reader.TryReadKey(out var key))
        {
            var advance = GetAdvance(key);

            if (advance != 0 && ContainsKey(key))
            {
                var glyph = _glyphs[key];
                if (glyph.HasBitmap)
                {
                    inkLeft = Math.Min(inkLeft, penX + glyph.Left);
                    inkRight = Math.Max(inkRight, penX + glyph.Right);
                    inkTop = Math.Min(inkTop, glyph.Top);
                    inkBottom = Math.Max(inkBottom, glyph.Bottom);
                }
            }

            penX += advance;
        }

        return inkRight == int.MinValue
            ? new LftTextMetrics(penX, 0, 0, 0, 0)
            : new LftTextMetrics(penX, inkLeft, inkTop, inkRight, inkBottom);
    }

    public void DrawGlyph(int key, Span<byte> mask, int stride, int height, int penX, int lineTop)
    {
        if (!IsDrawn(key) || !ContainsKey(key))
        {
            return;
        }

        var glyph = _glyphs[key];
        if (!glyph.HasBitmap)
        {
            return;
        }

        var rows = _data.Span.Slice(BitmapBlockStart + glyph.BitmapOffset, glyph.PackedSize);
        var rowStride = glyph.RowStride;
        var width = glyph.Width;

        for (var y = 0; y < glyph.Height; y++)
        {
            var targetY = lineTop + glyph.Top + y;
            if ((uint)targetY >= (uint)height)
            {
                continue;
            }

            var source = rows.Slice(y * rowStride, rowStride);
            var target = mask.Slice(targetY * stride, stride);

            for (var x = 0; x < width; x++)
            {
                if ((source[x >> 3] & (0x80 >> (x & 7))) == 0)
                {
                    continue;
                }

                var targetX = penX + glyph.Left + x;
                if ((uint)targetX < (uint)stride)
                {
                    target[targetX] = 255;
                }
            }
        }
    }

    public int DrawText(scoped ReadOnlySpan<byte> text, LftCodePage codePage, Span<byte> mask, int stride, int height, int penX, int lineTop)
    {
        var reader = new LftKeyReader(text, codePage);
        while (reader.TryReadKey(out var key))
        {
            DrawGlyph(key, mask, stride, height, penX, lineTop);
            penX += GetAdvance(key);
        }

        return penX;
    }
}
