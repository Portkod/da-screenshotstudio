using DaScreenshotStudio.Assets.Tables;

namespace DaScreenshotStudio.Assets.Characters;

public sealed class CharacterPaletteTable
{
    public const int DefaultPaletteNumber = 0;

    public const int ResourceIdCount = 1000;

    private const int MaleOnly = -1;

    private const int FemaleOnly = -2;

    private const int BothGenders = 0;

    private readonly int[] _male;
    private readonly int[] _female;

    private CharacterPaletteTable(int[] male, int[] female)
    {
        _male = male;
        _female = female;
    }

    public static CharacterPaletteTable Empty { get; } = new([], []);

    public static CharacterPaletteTable Parse(ReadOnlySpan<byte> data)
    {
        var highest = -1;
        var reader = new TableReader(data);
        Span<int> parts = stackalloc int[3];

        while (reader.TryReadLine(out var line))
        {
            if (TryReadRow(ref line, parts, out var row) && row.Last > highest)
            {
                highest = row.Last;
            }
        }

        if (highest < 0)
        {
            return Empty;
        }

        var male = new int[highest + 1];
        var female = new int[highest + 1];

        reader = new TableReader(data);
        while (reader.TryReadLine(out var line))
        {
            if (!TryReadRow(ref line, parts, out var row))
            {
                continue;
            }

            for (var id = row.First; id <= row.Last; id++)
            {
                if (row.Male)
                {
                    male[id] = row.PaletteNumber;
                }

                if (row.Female)
                {
                    female[id] = row.PaletteNumber;
                }
            }
        }

        return new CharacterPaletteTable(male, female);
    }

    public int GetPaletteNumber(int resourceId, bool female)
    {
        var table = female ? _female : _male;
        return (uint)resourceId < (uint)table.Length ? table[resourceId] : DefaultPaletteNumber;
    }

    private readonly record struct Row(int First, int Last, int PaletteNumber, bool Male, bool Female);

    private static bool TryReadRow(ref TableLine line, scoped Span<int> parts, out Row row)
    {
        row = default;

        var count = line.ReadInts(parts);
        if (count < 2)
        {
            return false;
        }

        var first = parts[0];
        var last = count == 2 ? parts[0] : parts[1];
        var third = count == 2 ? parts[1] : parts[2];

        int paletteNumber;
        bool male;
        bool female;

        if (third > 0)
        {
            paletteNumber = third;
            male = true;
            female = true;
        }
        else
        {
            paletteNumber = last;
            male = third is BothGenders or MaleOnly;
            female = third is BothGenders or FemaleOnly;

            if (!male && !female)
            {
                return false;
            }
        }

        if (last < first)
        {
            last = first;
        }

        if ((uint)first >= ResourceIdCount || (uint)last >= ResourceIdCount || paletteNumber < 0)
        {
            return false;
        }

        row = new Row(first, last, paletteNumber, male, female);
        return true;
    }
}
