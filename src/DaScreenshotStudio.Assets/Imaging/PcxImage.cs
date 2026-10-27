using System.Buffers.Binary;
using DaScreenshotStudio.Assets.Palettes;

namespace DaScreenshotStudio.Assets.Imaging;

public sealed class PcxImage
{
    public const int HeaderLength = 128;

    public const int PaletteTailLength = 1 + Palette.FileLength;

    public const byte PaletteMarker = 0x0C;

    public const byte ZSoftManufacturer = 0x0A;

    private const int RunMask = 0xC0;

    private const int MaxRun = 0x3F;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly int _bodyStart;
    private readonly int _bodyLength;
    private readonly int _paletteStart;

    private PcxImage(
        ReadOnlyMemory<byte> data,
        int bodyStart,
        int bodyLength,
        int paletteStart,
        int width,
        int height,
        byte version,
        byte encoding,
        byte bitsPerPixel,
        byte planeCount,
        int bytesPerLine)
    {
        _data = data;
        _bodyStart = bodyStart;
        _bodyLength = bodyLength;
        _paletteStart = paletteStart;
        Width = width;
        Height = height;
        Version = version;
        Encoding = encoding;
        BitsPerPixel = bitsPerPixel;
        PlaneCount = planeCount;
        BytesPerLine = bytesPerLine;
    }

    public int Width { get; }

    public int Height { get; }

    public byte Version { get; }

    public byte Encoding { get; }

    public byte BitsPerPixel { get; }

    public byte PlaneCount { get; }

    public int BytesPerLine { get; }

    public int IndexCount => BytesPerLine * Height;

    public ReadOnlySpan<byte> Body => _data.Span.Slice(_bodyStart, _bodyLength);

    public ReadOnlySpan<byte> PaletteBytes => _data.Span.Slice(_paletteStart + 1, Palette.FileLength);

    public Palette ReadPalette() => Palette.Parse(PaletteBytes);

    public static PcxImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < HeaderLength + PaletteTailLength)
        {
            throw new InvalidDataException(
                $"A PCX needs at least {HeaderLength + PaletteTailLength} bytes; got {bytes.Length}.");
        }

        if (bytes[0] != ZSoftManufacturer)
        {
            throw new InvalidDataException($"A PCX starts with 0x0A; this starts with 0x{bytes[0]:X2}.");
        }

        int minX = BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]);
        int minY = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]);
        int maxX = BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]);
        int maxY = BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]);

        var bitsPerPixel = bytes[3];
        var planeCount = bytes[65];
        int bytesPerLine = BinaryPrimitives.ReadUInt16LittleEndian(bytes[66..]);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;

        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"A PCX declares a {width} by {height} image.");
        }

        if (bitsPerPixel != 8 || planeCount != 1)
        {
            throw new InvalidDataException(
                $"A PCX with {bitsPerPixel} bits over {planeCount} planes is not the single 8 bit form the client ships.");
        }

        if (bytesPerLine < width)
        {
            throw new InvalidDataException(
                $"A PCX row of {bytesPerLine} bytes is narrower than its {width} pixel image.");
        }

        var paletteStart = bytes.Length - PaletteTailLength;
        if (bytes[paletteStart] != PaletteMarker)
        {
            throw new InvalidDataException(
                $"An 8 bit PCX carries a 0x0C palette marker at {paletteStart}; this holds 0x{bytes[paletteStart]:X2}.");
        }

        var indexCount = (long)bytesPerLine * height;
        var decodable = (long)(paletteStart - HeaderLength) * MaxRun;
        if (indexCount > decodable)
        {
            throw new InvalidDataException(
                $"A PCX declares {indexCount} indexes, more than the {decodable} its body could expand to.");
        }

        return new PcxImage(
            data,
            HeaderLength,
            paletteStart - HeaderLength,
            paletteStart,
            width,
            height,
            bytes[1],
            bytes[2],
            bitsPerPixel,
            planeCount,
            bytesPerLine);
    }

    public int Decode(Span<byte> destination)
    {
        var expected = IndexCount;
        if (destination.Length < expected)
        {
            throw new ArgumentException(
                $"A PCX decodes to {expected} bytes, more than the {destination.Length} given.",
                nameof(destination));
        }

        var source = Body;
        var written = 0;

        for (var i = 0; i < source.Length && written < expected;)
        {
            var control = source[i++];
            if ((control & RunMask) != RunMask)
            {
                destination[written++] = control;
                continue;
            }

            if (i >= source.Length)
            {
                throw new InvalidDataException("A PCX run length byte sits at the end of its body.");
            }

            var run = Math.Min(control & MaxRun, expected - written);
            destination.Slice(written, run).Fill(source[i++]);
            written += run;
        }

        if (written != expected)
        {
            throw new InvalidDataException($"A PCX decoded {written} of its {expected} bytes.");
        }

        return written;
    }

    public byte[] Decode()
    {
        var indexes = new byte[IndexCount];
        Decode(indexes);
        return indexes;
    }
}
