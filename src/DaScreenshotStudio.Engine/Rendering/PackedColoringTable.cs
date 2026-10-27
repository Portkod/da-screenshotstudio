using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Engine.Rendering;

public sealed class PackedColoringTable
{
    private readonly ushort[] _colors;
    private readonly bool[] _present;

    private PackedColoringTable(ushort[] colors, bool[] present)
    {
        _colors = colors;
        _present = present;
    }

    public const string EntryName = "color0.tbl";

    public static PackedColoringTable? TryLoad(IAssetArchive? archive, in PixelPacking packing)
    {
        if (archive is null || !archive.TryRead(EntryName, out var data))
        {
            return null;
        }

        ColoringTable table;
        try
        {
            table = ColoringTable.Parse(data);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return Pack(table, in packing);
    }

    public static PackedColoringTable Pack(ColoringTable table, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(table);

        var colors = new ushort[ColoringTable.MaxTuples * ColoringTable.DyeLength];
        var present = new bool[ColoringTable.MaxTuples];

        for (var number = 0; number < ColoringTable.MaxTuples; number++)
        {
            var tuple = table.GetTuple(number);
            if (tuple.IsEmpty)
            {
                continue;
            }

            var length = Math.Min(tuple.Length, ColoringTable.DyeLength);
            for (var i = 0; i < length; i++)
            {
                var color = packing.Pack(tuple[i].Red, tuple[i].Green, tuple[i].Blue);
                colors[(number * ColoringTable.DyeLength) + i] =
                    color == PalettePacker.TransparentKey ? PalettePacker.BumpedBlack : color;
            }

            present[number] = true;
        }

        return new PackedColoringTable(colors, present);
    }

    public ReadOnlySpan<ushort> Get(int dye) =>
        (uint)dye < ColoringTable.MaxTuples && _present[dye]
            ? _colors.AsSpan(dye * ColoringTable.DyeLength, ColoringTable.DyeLength)
            : default;
}
