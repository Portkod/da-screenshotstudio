using System.Buffers;
using System.IO.Compression;

namespace DaScreenshotStudio.Assets.Imaging;

internal static class ZlibPayload
{
    public static int Inflate(scoped ReadOnlySpan<byte> compressed, Span<byte> destination, int decodedLength)
    {
        if (compressed.IsEmpty)
        {
            return 0;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(compressed.Length);
        try
        {
            compressed.CopyTo(buffer);

            using var source = new MemoryStream(buffer, 0, compressed.Length, writable: false);
            using var inflate = new ZLibStream(source, CompressionMode.Decompress);

            var written = 0;
            while (written < decodedLength)
            {
                var read = inflate.Read(destination[written..decodedLength]);
                if (read == 0)
                {
                    break;
                }

                written += read;
            }

            return written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static byte[] Inflate(scoped ReadOnlySpan<byte> compressed, int decodedLength)
    {
        var decoded = new byte[decodedLength];
        var written = Inflate(compressed, decoded, decodedLength);

        return written == decoded.Length ? decoded : decoded[..written];
    }
}
