using System.Globalization;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Staging;

public static class MapCandidates
{
    public const int MaxSide = 255;

    public readonly record struct Candidate(int Map, int Width, int Height);

    public static IEnumerable<int> MapIds(string mapsPath)
    {
        foreach (var path in Directory.EnumerateFiles(mapsPath, "lod*.map"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (int.TryParse(name.AsSpan(3), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                yield return id;
            }
        }
    }

    public static IEnumerable<(int Width, int Height)> Shapes(int cells)
    {
        for (var width = 1; width <= MaxSide; width++)
        {
            if (cells % width == 0 && cells / width is var height and <= MaxSide)
            {
                yield return (width, height);
            }
        }
    }

    public static IEnumerable<Candidate> Enumerate(string mapsPath, int x, int y)
    {
        foreach (var id in MapIds(mapsPath).Order())
        {
            var length = new FileInfo(Path.Combine(mapsPath, $"lod{id}.map")).Length;
            if (length % MapCell.Length != 0)
            {
                continue;
            }

            foreach (var (width, height) in Shapes((int)(length / MapCell.Length)))
            {
                if (x < width && y < height)
                {
                    yield return new Candidate(id, width, height);
                }
            }
        }
    }

    public static double WorldCorrelation(Screenshot expected, Screenshot actual, PixelRect world)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        var left = Math.Max(0, world.X);
        var top = Math.Max(0, world.Y);
        var right = Math.Min(expected.Width, world.X + world.Width);
        var bottom = Math.Min(expected.Height, world.Y + world.Height);
        double n = 0, sumA = 0, sumB = 0, sumAA = 0, sumBB = 0, sumAB = 0;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var a = Brightness(expected[x, y]);
                var b = Brightness(actual[x, y]);
                n++;
                sumA += a;
                sumB += b;
                sumAA += a * a;
                sumBB += b * b;
                sumAB += a * b;
            }
        }

        var varianceA = (n * sumAA) - (sumA * sumA);
        var varianceB = (n * sumBB) - (sumB * sumB);
        return varianceA <= 0 || varianceB <= 0 ? 0 : ((n * sumAB) - (sumA * sumB)) / Math.Sqrt(varianceA * varianceB);
    }

    private static int Brightness(ushort rgb555) =>
        (((rgb555 >> 10) & 0x1F) * 3) + (((rgb555 >> 5) & 0x1F) * 6) + (rgb555 & 0x1F);

    public static IEnumerable<(int X, int Y)> CameraSlides()
    {
        yield return (0, 0);
        for (var k = 1; k <= 7; k++)
        {
            yield return (4 * k, 2 * k);
            yield return (-4 * k, -2 * k);
            yield return (4 * k, -2 * k);
            yield return (-4 * k, 2 * k);
        }
    }

    public static (double Score, int SlideX, int SlideY) BestSlide(Screenshot expected, Screenshot actual, PixelRect world)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        const int margin = 28;
        var best = (Score: -1.0, SlideX: 0, SlideY: 0);

        foreach (var (sx, sy) in CameraSlides())
        {
            long same = 0, total = 0;
            for (var y = world.Y + margin; y < world.Bottom - margin; y += 2)
            {
                for (var x = world.X + margin; x < world.Right - margin; x += 2)
                {
                    total++;
                    if (expected[x, y] == actual[x + sx, y + sy])
                    {
                        same++;
                    }
                }
            }

            var score = total == 0 ? 0 : (double)same / total;
            if (score > best.Score)
            {
                best = (score, sx, sy);
            }
        }

        return best;
    }
}
