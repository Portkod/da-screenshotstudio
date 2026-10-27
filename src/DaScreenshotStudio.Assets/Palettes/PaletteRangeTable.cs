using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Palettes;

public sealed class PaletteRangeTable
{
    public const int Unmapped = -1;

    private const int IndexLimit = 1 << 20;

    private readonly int[] _paletteNumbers;

    private PaletteRangeTable(int[] paletteNumbers) => _paletteNumbers = paletteNumbers;

    public static PaletteRangeTable Empty { get; } = new([]);

    public static PaletteRangeTable Parse(ReadOnlySpan<byte> data)
    {
        var highest = -1;
        var reader = new TableReader(data);
        Span<int> parts = stackalloc int[3];

        while (reader.TryReadLine(out var line))
        {
            if (TryReadRange(ref line, parts, out _, out var last, out _) && last > highest)
            {
                highest = last;
            }
        }

        if (highest < 0)
        {
            return Empty;
        }

        var numbers = new int[highest + 1];
        numbers.AsSpan().Fill(Unmapped);

        reader = new TableReader(data);
        while (reader.TryReadLine(out var line))
        {
            if (!TryReadRange(ref line, parts, out var first, out var last, out var paletteNumber))
            {
                continue;
            }

            numbers.AsSpan(first, last - first + 1).Fill(paletteNumber);
        }

        return new PaletteRangeTable(numbers);
    }

    public int GetPaletteNumber(int assetIndex) =>
        (uint)assetIndex < (uint)_paletteNumbers.Length ? _paletteNumbers[assetIndex] : Unmapped;

    private static bool TryReadRange(ref TableLine line, scoped Span<int> parts, out int first, out int last, out int paletteNumber)
    {
        first = 0;
        last = 0;
        paletteNumber = 0;

        switch (line.ReadInts(parts))
        {
            case 2:
                first = parts[0] - 1;
                last = first;
                paletteNumber = parts[1];
                break;
            case 3:
                first = parts[0] - 1;
                last = parts[1] - 1;
                paletteNumber = parts[2];
                break;
            default:
                return false;
        }

        return first >= 0 && last >= first && last < IndexLimit;
    }
}
