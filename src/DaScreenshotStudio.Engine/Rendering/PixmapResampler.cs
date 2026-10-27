namespace DaScreenshotStudio.Engine.Rendering;

public static class PixmapResampler
{
    public static int HalvedPixelCount(in Pixmap source) => HalvedWidth(in source) * HalvedHeight(in source);

    public static int HalvedWidth(in Pixmap source) => source.Width / 2;

    public static int HalvedHeight(in Pixmap source) => source.Height / 2;

    public static Pixmap Halve(in Pixmap source, PixelBlender blender, ushort[] destination)
    {
        ArgumentNullException.ThrowIfNull(blender);
        ArgumentNullException.ThrowIfNull(destination);

        var width = HalvedWidth(in source);
        var height = HalvedHeight(in source);
        if (width <= 0 || height <= 0)
        {
            return default;
        }

        if (destination.Length < width * height)
        {
            throw new ArgumentException("The destination is shorter than the halved pixmap.", nameof(destination));
        }

        for (var y = 0; y < height; y++)
        {
            var top = source.Row(y * 2);
            var bottom = source.Row((y * 2) + 1);
            var row = destination.AsSpan(y * width, width);

            for (var x = 0; x < width; x++)
            {
                var left = x * 2;
                row[x] = blender.Average(
                    blender.Average(top[left], top[left + 1]),
                    blender.Average(bottom[left], bottom[left + 1]));
            }
        }

        return new Pixmap(destination.AsMemory(0, width * height), width, height);
    }
}
