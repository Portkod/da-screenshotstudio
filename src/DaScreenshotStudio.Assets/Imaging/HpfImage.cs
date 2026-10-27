namespace DaScreenshotStudio.Assets.Imaging;

public sealed class HpfImage
{
    public const int Width = 28;

    public const int HeaderLength = 8;

    private readonly byte[] _decoded;

    private HpfImage(byte[] decoded, bool wasCompressed)
    {
        _decoded = decoded;
        WasCompressed = wasCompressed;
        Height = (decoded.Length - HeaderLength) / Width;
    }

    public int Height { get; }

    public bool WasCompressed { get; }

    public ReadOnlySpan<byte> Pixels => _decoded.AsSpan(HeaderLength, Height * Width);

    public static HpfImage Parse(ReadOnlySpan<byte> data)
    {
        var compressed = HpfCodec.IsCompressed(data);
        var decoded = HpfCodec.Decode(data);

        if (decoded.Length < HeaderLength)
        {
            throw new InvalidDataException($"An HPF image decoded to {decoded.Length} bytes, shorter than its {HeaderLength} byte header.");
        }

        return new HpfImage(decoded, compressed);
    }
}
