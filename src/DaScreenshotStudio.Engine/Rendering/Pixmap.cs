namespace DaScreenshotStudio.Engine.Rendering;

public readonly struct Pixmap
{
    private readonly ReadOnlyMemory<ushort> _pixels;

    public Pixmap(ReadOnlyMemory<ushort> pixels, int width, int height)
        : this(pixels, width, height, width)
    {
    }

    public Pixmap(ReadOnlyMemory<ushort> pixels, int width, int height, int stride)
    {
        if (width < 0 || height < 0 || stride < width)
        {
            throw new ArgumentException("A pixmap needs a nonnegative size and a stride at least its width.");
        }

        if (height > 0 && pixels.Length < ((height - 1) * stride) + width)
        {
            throw new ArgumentException("A pixmap's storage is shorter than its bounds.", nameof(pixels));
        }

        _pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public ReadOnlySpan<ushort> Row(int y) => _pixels.Span.Slice(y * Stride, Width);

    public Pixmap Rows(int top, int height)
    {
        if (top < 0 || height < 0 || top + height > Height)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "The row range lies outside the pixmap.");
        }

        return height == 0 ? default : new Pixmap(_pixels[(top * Stride)..], Width, height, Stride);
    }

    public ReadOnlySpan<ushort> Pixels => _pixels.Span;
}
