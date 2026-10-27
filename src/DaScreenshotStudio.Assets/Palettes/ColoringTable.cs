namespace DaScreenshotStudio.Assets.Palettes;

public sealed class ColoringTable
{
    public const int MaxTuples = 256;

    public const int DyeFirstIndex = 0x62;

    public const int DyeLength = 6;

    private readonly PaletteColor[] _colors;
    private readonly bool[] _present;

    private ColoringTable(int width, PaletteColor[] colors, bool[] present)
    {
        Width = width;
        _colors = colors;
        _present = present;
    }

    public int Width { get; }

    public static ColoringTable Parse(ReadOnlySpan<byte> data)
    {
        var position = 0;
        if (!TryReadDecimal(data, ref position, out var width) || width <= 0)
        {
            throw new InvalidDataException("A coloring table starts with its tuple width.");
        }

        var colors = new PaletteColor[MaxTuples * width];
        var present = new bool[MaxTuples];

        while (TryReadDecimal(data, ref position, out var number))
        {
            if ((uint)number >= MaxTuples)
            {
                break;
            }

            for (var i = 0; i < width; i++)
            {
                if (!TryReadDecimal(data, ref position, out var red)
                    || !TryReadDecimal(data, ref position, out var green)
                    || !TryReadDecimal(data, ref position, out var blue))
                {
                    return new ColoringTable(width, colors, present);
                }

                colors[(number * width) + i] = new PaletteColor((byte)red, (byte)green, (byte)blue);
            }

            present[number] = true;
        }

        return new ColoringTable(width, colors, present);
    }

    public ReadOnlySpan<PaletteColor> GetTuple(int number) =>
        (uint)number < MaxTuples && _present[number]
            ? _colors.AsSpan(number * Width, Width)
            : default;

    private static bool TryReadDecimal(ReadOnlySpan<byte> data, ref int position, out int value)
    {
        while (position < data.Length && (data[position] < (byte)'0' || data[position] > (byte)'9'))
        {
            position++;
        }

        if (position >= data.Length)
        {
            value = 0;
            return false;
        }

        value = 0;
        while (position < data.Length && data[position] >= (byte)'0' && data[position] <= (byte)'9')
        {
            value = (value * 10) + (data[position] - (byte)'0');
            position++;
        }

        return true;
    }
}
