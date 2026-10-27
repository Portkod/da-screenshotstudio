namespace DaScreenshotStudio.Engine.Rendering;

public sealed class PixelSurface
{
    public const int PitchAlignment = 4;

    private ushort[] _pixels;

    public PixelSurface(int width, int height)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        Pitch = AlignPitch(Width);
        _pixels = new ushort[Math.Max(1, Pitch * Height)];
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int Pitch { get; private set; }

    public Span<ushort> Pixels => _pixels.AsSpan(0, Pitch * Height);

    public ReadOnlySpan<ushort> ReadOnlyPixels => _pixels.AsSpan(0, Pitch * Height);

    public Span<ushort> Row(int y) => _pixels.AsSpan(y * Pitch, Width);

    public static int AlignPitch(int width) => (width + PitchAlignment - 1) & ~(PitchAlignment - 1);

    public void Resize(int width, int height)
    {
        width = Math.Max(0, width);
        height = Math.Max(0, height);

        var pitch = AlignPitch(width);
        var needed = Math.Max(1, pitch * height);

        if (needed > _pixels.Length)
        {
            _pixels = new ushort[needed];
        }

        Width = width;
        Height = height;
        Pitch = pitch;
    }
}
