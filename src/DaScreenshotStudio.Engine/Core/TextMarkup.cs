namespace DaScreenshotStudio.Engine.Core;

public enum MarkupSegmentKind
{
    Text,

    PaletteChange,
}

public readonly ref struct MarkupSegment
{
    internal MarkupSegment(MarkupSegmentKind kind, ReadOnlySpan<byte> text, byte paletteIndex)
    {
        Kind = kind;
        Text = text;
        PaletteIndex = paletteIndex;
    }

    public MarkupSegmentKind Kind { get; }

    public ReadOnlySpan<byte> Text { get; }

    public byte PaletteIndex { get; }
}

public static class TextMarkup
{
    public const byte Open = (byte)'{';

    public const byte EqualsSign = (byte)'=';

    public const int CodeLength = 3;

    public const byte FirstLetter = (byte)'a';

    public const byte LastLetter = (byte)'x';

    public const byte White = 0xFF;

    public const byte Yellow = 0x45;

    public const byte Chant = 0x58;

    public const byte MessageHistory = 0x97;

    private static ReadOnlySpan<byte> PaletteIndexes =>
    [
        0x14, 0x28, 0x45, 0x89, 0x58, 0x5C, 0x12, 0x14,
        0x16, 0x18, 0x1A, 0x1C, 0x1E, 0x1F, 0x24, 0x6D,
        0x80, 0x89, 0x97, 0xA7, 0xFF, 0x58, 0x24, 0x00,
    ];

    public static bool IsCodeLetter(byte letter) => letter is >= FirstLetter and <= LastLetter;

    public static bool TryGetPaletteIndex(byte letter, out byte paletteIndex)
    {
        if (!IsCodeLetter(letter))
        {
            paletteIndex = 0;
            return false;
        }

        paletteIndex = PaletteIndexes[letter - FirstLetter];
        return true;
    }

    public static bool StartsWithCode(scoped ReadOnlySpan<byte> text, out byte paletteIndex)
    {
        if (text.Length >= CodeLength && text[0] == Open && text[1] == EqualsSign)
        {
            return TryGetPaletteIndex(text[2], out paletteIndex);
        }

        paletteIndex = 0;
        return false;
    }

    public static int StrippedLength(scoped ReadOnlySpan<byte> text)
    {
        var length = 0;
        var reader = new MarkupReader(text);

        while (reader.TryRead(out var segment))
        {
            if (segment.Kind == MarkupSegmentKind.Text)
            {
                length += segment.Text.Length;
            }
        }

        return length;
    }
}

public ref struct MarkupReader
{
    private ReadOnlySpan<byte> _remaining;
    private byte _pendingPalette;
    private bool _pendingIsPalette;

    public MarkupReader(ReadOnlySpan<byte> text) => _remaining = text;

    public bool TryRead(out MarkupSegment segment)
    {
        if (_pendingIsPalette)
        {
            _pendingIsPalette = false;
            segment = new MarkupSegment(MarkupSegmentKind.PaletteChange, default, _pendingPalette);
            return true;
        }

        if (_remaining.IsEmpty)
        {
            segment = default;
            return false;
        }

        for (var i = 0; i < _remaining.Length; i++)
        {
            if (_remaining[i] != TextMarkup.Open || !TextMarkup.StartsWithCode(_remaining[i..], out var palette))
            {
                continue;
            }

            var text = _remaining[..i];
            _remaining = _remaining[(i + TextMarkup.CodeLength)..];

            if (i == 0)
            {
                segment = new MarkupSegment(MarkupSegmentKind.PaletteChange, default, palette);
                return true;
            }

            _pendingPalette = palette;
            _pendingIsPalette = true;
            segment = new MarkupSegment(MarkupSegmentKind.Text, text, 0);
            return true;
        }

        segment = new MarkupSegment(MarkupSegmentKind.Text, _remaining, 0);
        _remaining = default;
        return true;
    }
}
