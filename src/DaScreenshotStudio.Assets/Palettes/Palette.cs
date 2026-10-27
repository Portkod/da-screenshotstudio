using System.Runtime.InteropServices;

namespace DaScreenshotStudio.Assets.Palettes;

public sealed class Palette
{
    public const int ColorCount = 256;

    public const int FileLength = ColorCount * 3;

    private readonly PaletteColor[] _colors;

    private Palette(PaletteColor[] colors) => _colors = colors;

    public PaletteColor this[int index] => _colors[index];

    public ReadOnlySpan<PaletteColor> Colors => _colors;

    public static Palette Parse(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith("RIFF"u8))
        {
            throw new InvalidDataException("This is a RIFF PAL resource, not the raw 768 byte palette the client reads.");
        }

        if (data.Length < FileLength)
        {
            throw new InvalidDataException($"A palette is {FileLength} bytes; got {data.Length}.");
        }

        var colors = new PaletteColor[ColorCount];
        MemoryMarshal.Cast<byte, PaletteColor>(data[..FileLength]).CopyTo(colors);

        return new Palette(colors);
    }

    public static Palette Grayscale()
    {
        var colors = new PaletteColor[ColorCount];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = new PaletteColor((byte)i, (byte)i, (byte)i);
        }

        return new Palette(colors);
    }
}
