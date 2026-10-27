using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Engine.Rendering;

public static class IndexedPixmap
{
    public const byte TruncatedChannelMask = 0xF8;

    public static Pixmap FromPcx(PcxImage image, in PixelPacking packing, Span<ushort> colors)
    {
        ArgumentNullException.ThrowIfNull(image);

        return Pack(
            image.Decode(),
            image.Width,
            image.Height,
            image.BytesPerLine,
            image.PaletteBytes,
            TruncatedChannelMask,
            in packing,
            colors);
    }

    public static Pixmap Pack(
        ReadOnlySpan<byte> indexes,
        int width,
        int height,
        int stride,
        ReadOnlySpan<byte> palette,
        byte channelMask,
        in PixelPacking packing,
        Span<ushort> colors)
    {
        if (width <= 0 || height <= 0)
        {
            return default;
        }

        if (stride < width)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                stride,
                $"A row of {stride} bytes is narrower than its {width} pixel image.");
        }

        if (indexes.Length < ((height - 1) * stride) + width)
        {
            throw new ArgumentException(
                $"A {width} by {height} image over a {stride} byte row needs more than {indexes.Length} indexes.",
                nameof(indexes));
        }

        if (palette.Length < Palette.FileLength)
        {
            throw new ArgumentException(
                $"A palette is {Palette.FileLength} bytes; got {palette.Length}.",
                nameof(palette));
        }

        if (colors.Length < Palette.ColorCount)
        {
            throw new ArgumentException(
                $"The color scratch holds {Palette.ColorCount} entries; got {colors.Length}.",
                nameof(colors));
        }

        for (var i = 0; i < Palette.ColorCount; i++)
        {
            colors[i] = packing.Pack(
                (byte)(palette[i * 3] & channelMask),
                (byte)(palette[(i * 3) + 1] & channelMask),
                (byte)(palette[(i * 3) + 2] & channelMask));
        }

        var pixels = new ushort[width * height];

        for (var y = 0; y < height; y++)
        {
            var row = indexes.Slice(y * stride, width);
            var destination = pixels.AsSpan(y * width, width);

            for (var x = 0; x < width; x++)
            {
                destination[x] = colors[row[x]];
            }
        }

        return new Pixmap(pixels, width, height);
    }
}
