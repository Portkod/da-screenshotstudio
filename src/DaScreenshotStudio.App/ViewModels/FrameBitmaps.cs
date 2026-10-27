using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Staging;

namespace DaScreenshotStudio.App.ViewModels;

/// <summary>How the canvas shows the render against the reference screenshot.</summary>
public enum OverlayMode
{
    Scene,
    Reference,
    Difference,
    Blend,
}

/// <summary>Turns packed client pixels into bitmaps Avalonia can draw.</summary>
internal static class FrameBitmaps
{
    private static readonly uint[] Rgb565 = BuildTable(rgb555: false);
    private static readonly uint[] Rgb555 = BuildTable(rgb555: true);

    private static uint[] BuildTable(bool rgb555)
    {
        var table = new uint[1 << 16];
        for (var value = 0; value < table.Length; value++)
        {
            int r, g, b;
            if (rgb555)
            {
                r = ((value >> 10) & 0x1F) << 3;
                g = ((value >> 5) & 0x1F) << 3;
                b = (value & 0x1F) << 3;
            }
            else
            {
                r = ((value >> 11) & 0x1F) << 3;
                g = ((value >> 5) & 0x3F) << 2;
                b = (value & 0x1F) << 3;
            }

            table[value] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }

        return table;
    }

    /// <summary>Writes a frame, as the screenshot stores it, into a bitmap with the chosen overlay.</summary>
    public static unsafe void Compose(
        WriteableBitmap target, Screenshot frame, Screenshot? reference, OverlayMode mode, double blend)
    {
        using var locked = target.Lock();
        var width = frame.Width;
        var height = frame.Height;
        var alpha = (int)Math.Round(Math.Clamp(blend, 0, 1) * 256);

        for (var y = 0; y < height; y++)
        {
            var row = new Span<uint>((byte*)locked.Address + (y * locked.RowBytes), width);
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                var rendered = Rgb555[frame.Pixels[i]];

                if (reference is null || mode == OverlayMode.Scene)
                {
                    row[x] = rendered;
                    continue;
                }

                var expected = Rgb555[reference.Pixels[i]];
                row[x] = mode switch
                {
                    OverlayMode.Reference => expected,
                    OverlayMode.Difference => frame.Pixels[i] == reference.Pixels[i] ? Dim(rendered) : 0xFFFF00FFu,
                    _ => Mix(rendered, expected, alpha),
                };
            }
        }
    }

    /// <summary>A packed 565 pixmap with index 0 transparent, as a bitmap.</summary>
    public static unsafe WriteableBitmap? FromPixmap(in Pixmap pixmap)
    {
        if (pixmap.IsEmpty)
        {
            return null;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(pixmap.Width, pixmap.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

        using var locked = bitmap.Lock();
        for (var y = 0; y < pixmap.Height; y++)
        {
            var source = pixmap.Row(y);
            var row = new Span<uint>((byte*)locked.Address + (y * locked.RowBytes), pixmap.Width);
            for (var x = 0; x < pixmap.Width; x++)
            {
                row[x] = source[x] == 0 ? 0u : Rgb565[source[x]];
            }
        }

        return bitmap;
    }

    public static WriteableBitmap CreateScreen() =>
        new(new PixelSize(SceneHost.ScreenWidth, SceneHost.ScreenHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

    private static uint Dim(uint pixel) =>
        0xFF000000u | ((pixel >> 2) & 0x003F3F3Fu);

    private static uint Mix(uint a, uint b, int alpha)
    {
        var inverse = 256 - alpha;
        var r = ((((a >> 16) & 0xFF) * inverse) + (((b >> 16) & 0xFF) * alpha)) >> 8;
        var g = ((((a >> 8) & 0xFF) * inverse) + (((b >> 8) & 0xFF) * alpha)) >> 8;
        var bl = (((a & 0xFF) * inverse) + ((b & 0xFF) * alpha)) >> 8;
        return 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)bl;
    }
}
