using DaScreenshotStudio.Assets.Metadata;

namespace DaScreenshotStudio.Assets.Maps;

public readonly record struct MapBottomPlacement(int ImageId, int Left, int Top);

public sealed class MapBottomPlacementTable
{
    private readonly MapBottomPlacement[] _placements;

    private MapBottomPlacementTable(MapBottomPlacement[] placements) => _placements = placements;

    public static MapBottomPlacementTable Empty { get; } = new([]);

    public int Count => _placements.Length;

    public ReadOnlySpan<MapBottomPlacement> Placements => _placements;

    public MapBottomPlacement this[int index] => _placements[index];

    public static MapBottomPlacementTable Parse(Metafile table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.GroupCount < 2)
        {
            throw new InvalidDataException(
                $"Bottom layer table '{table.Name}' holds {table.GroupCount} groups, and the client refuses fewer than 2.");
        }

        var declared = ReadLeadingInteger(table.GetGroupName(0));
        var available = table.GroupCount - 1;

        if (declared > available)
        {
            throw new InvalidDataException(
                $"Bottom layer table '{table.Name}' declares {declared} placements over {available} groups.");
        }

        if (declared <= 0)
        {
            return Empty;
        }

        var placements = new MapBottomPlacement[declared];
        for (var i = 0; i < declared; i++)
        {
            var record = table.GetGroupName(i + 1).AsSpan();
            var position = 0;

            TryReadInteger(record, ref position, out var imageId);
            TryReadInteger(record, ref position, out var left);
            TryReadInteger(record, ref position, out var top);

            placements[i] = new MapBottomPlacement(imageId, left, top);
        }

        return new MapBottomPlacementTable(placements);
    }

    private static int ReadLeadingInteger(scoped ReadOnlySpan<char> text)
    {
        var position = 0;
        TryReadInteger(text, ref position, out var value);
        return value;
    }

    private static bool TryReadInteger(scoped ReadOnlySpan<char> text, ref int position, out int value)
    {
        value = 0;
        var start = position;

        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        var negative = false;
        if (position < text.Length && (text[position] == '-' || text[position] == '+'))
        {
            negative = text[position] == '-';
            position++;
        }

        var digits = 0;
        var magnitude = 0;

        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            magnitude = unchecked((magnitude * 10) + (text[position] - '0'));
            position++;
            digits++;
        }

        if (digits == 0)
        {
            position = start;
            return false;
        }

        value = negative ? -magnitude : magnitude;
        return true;
    }
}
