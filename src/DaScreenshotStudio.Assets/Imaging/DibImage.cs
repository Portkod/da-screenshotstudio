using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public sealed class DibImage
{
    public const int FileHeaderLength = 14;

    public const int InfoHeaderLength = 40;

    public const uint Uncompressed = 0;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _pixelOffset;

    private DibImage(
        ReadOnlyMemory<byte> data,
        int pixelOffset,
        int width,
        int height,
        int bitsPerPixel,
        int planeCount,
        uint compression,
        int stride,
        bool isTopDown,
        uint declaredFileLength)
    {
        _data = data;
        _pixelOffset = pixelOffset;
        Width = width;
        Height = height;
        BitsPerPixel = bitsPerPixel;
        PlaneCount = planeCount;
        Compression = compression;
        Stride = stride;
        IsTopDown = isTopDown;
        DeclaredFileLength = declaredFileLength;
    }

    public int Width { get; }

    public int Height { get; }

    public int BitsPerPixel { get; }

    public int PlaneCount { get; }

    public uint Compression { get; }

    public int Stride { get; }

    public bool IsTopDown { get; }

    public uint DeclaredFileLength { get; }

    public static DibImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < FileHeaderLength + InfoHeaderLength)
        {
            throw new InvalidDataException(
                $"A DIB needs {FileHeaderLength + InfoHeaderLength} header bytes; got {bytes.Length}.");
        }

        if (bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
        {
            throw new InvalidDataException("A DIB starts with 'BM'.");
        }

        var declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[2..]);
        var pixelOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[10..]);
        var infoLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[14..]);

        if (infoLength < InfoHeaderLength)
        {
            throw new InvalidDataException($"A DIB info header is at least {InfoHeaderLength} bytes; this declares {infoLength}.");
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]);
        var storedHeight = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
        int planes = BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..]);
        int bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..]);
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes[30..]);

        if (compression != Uncompressed)
        {
            throw new InvalidDataException($"A DIB with compression {compression} is not a form the client reads.");
        }

        if (bitsPerPixel != 16 || planes != 1)
        {
            throw new InvalidDataException(
                $"A DIB with {bitsPerPixel} bits over {planes} planes is not the single 16 bit form the client ships.");
        }

        var height = Math.Abs((long)storedHeight);
        if (width <= 0 || height == 0)
        {
            throw new InvalidDataException($"A DIB declares a {width} by {storedHeight} image.");
        }

        var stride = (((long)width * bitsPerPixel) + 31) / 32 * 4;

        if (pixelOffset > (uint)bytes.Length
            || stride > bytes.Length
            || height > bytes.Length
            || pixelOffset + (stride * height) > bytes.Length)
        {
            throw new InvalidDataException(
                $"A DIB's {stride} byte rows from {pixelOffset} do not fit its {bytes.Length} bytes.");
        }

        return new DibImage(
            data,
            (int)pixelOffset,
            width,
            (int)height,
            bitsPerPixel,
            planes,
            compression,
            (int)stride,
            storedHeight < 0,
            declaredLength);
    }

    public ReadOnlySpan<byte> GetRow(int y)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        var row = IsTopDown ? y : Height - 1 - y;
        return _data.Span.Slice(_pixelOffset + (row * Stride), Stride);
    }
}
