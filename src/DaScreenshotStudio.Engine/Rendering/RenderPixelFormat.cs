namespace DaScreenshotStudio.Engine.Rendering;

public enum RenderPixelFormat
{
    Rgb565,
    Rgb555,
}

public readonly struct PixelPacking
{
    private PixelPacking(RenderPixelFormat format, int greenBits)
    {
        Format = format;
        GreenBits = greenBits;
        GreenShift = 5;
        RedShift = 5 + greenBits;
        RedMax = 31;
        GreenMax = (1 << greenBits) - 1;
        BlueMax = 31;
    }

    public RenderPixelFormat Format { get; }

    public int RedShift { get; }

    public int GreenShift { get; }

    public int GreenBits { get; }

    public int RedMax { get; }

    public int GreenMax { get; }

    public int BlueMax { get; }

    public static PixelPacking Rgb565 { get; } = new(RenderPixelFormat.Rgb565, 6);

    public static PixelPacking Rgb555 { get; } = new(RenderPixelFormat.Rgb555, 5);

    public static PixelPacking For(RenderPixelFormat format) =>
        format == RenderPixelFormat.Rgb555 ? Rgb555 : Rgb565;

    public ushort Pack(byte red, byte green, byte blue) => (ushort)(
        ((red >> 3) << RedShift)
        | ((green >> (8 - GreenBits)) << GreenShift)
        | (blue >> 3));

    public byte ExpandRed(ushort pixel) => (byte)(Red(pixel) << 3);

    public byte ExpandGreen(ushort pixel) => (byte)(Green(pixel) << (8 - GreenBits));

    public byte ExpandBlue(ushort pixel) => (byte)(Blue(pixel) << 3);

    public int Red(ushort pixel) => (pixel >> RedShift) & RedMax;

    public int Green(ushort pixel) => (pixel >> GreenShift) & GreenMax;

    public int Blue(ushort pixel) => pixel & BlueMax;

    public ushort Compose(int red, int green, int blue) =>
        (ushort)((red << RedShift) | (green << GreenShift) | blue);
}
