using System.Buffers;
using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public static class HpfCodec
{
    public const uint Magic = 0xFF02AA55;

    private const int SymbolCount = 256;
    private const int EndMarker = 256;
    private const int NodeCount = (2 * SymbolCount) + 1;

    public static bool IsCompressed(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic;

    public static byte[] Decode(ReadOnlySpan<byte> data)
    {
        if (!IsCompressed(data))
        {
            return data.ToArray();
        }

        var stream = data[4..];
        var capacity = Math.Max(stream.Length * 10, 4096);

        while (true)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(capacity);
            try
            {
                var written = DecodeTree(stream, buffer, out var overflowed);
                if (!overflowed)
                {
                    return buffer.AsSpan(0, written).ToArray();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            capacity *= 4;
            if (capacity > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("HPF stream did not reach its end marker before 64 MB of output.");
            }
        }
    }

    private static int DecodeTree(ReadOnlySpan<byte> stream, Span<byte> destination, out bool overflowed)
    {
        Span<ushort> left = stackalloc ushort[SymbolCount];
        Span<ushort> right = stackalloc ushort[SymbolCount];
        Span<byte> parent = stackalloc byte[NodeCount];

        for (var i = 0; i < SymbolCount; i++)
        {
            var leftChild = (2 * i) + 1;
            left[i] = (ushort)leftChild;
            right[i] = (ushort)(leftChild + 1);
            parent[leftChild] = (byte)i;
            parent[leftChild + 1] = (byte)i;
        }

        overflowed = false;
        var bitPosition = 0;
        var written = 0;
        var bitCount = stream.Length * 8;

        while (true)
        {
            var node = 0;
            while (node < SymbolCount)
            {
                if (bitPosition >= bitCount)
                {
                    return written;
                }

                var bit = (stream[bitPosition >> 3] >> (bitPosition & 7)) & 1;
                bitPosition++;
                node = bit == 0 ? left[node] : right[node];
            }

            Rotate(left, right, parent, node);

            var symbol = node - SymbolCount;
            if (symbol == EndMarker)
            {
                return written;
            }

            if (written >= destination.Length)
            {
                overflowed = true;
                return written;
            }

            destination[written++] = (byte)symbol;
        }
    }

    private static void Rotate(Span<ushort> left, Span<ushort> right, Span<byte> parent, int leaf)
    {
        var current = leaf;
        int ancestor = parent[current];
        var steps = 0;

        while (current != 0 && ancestor != 0)
        {
            if (++steps > NodeCount)
            {
                throw new InvalidDataException("An HPF tree rotation did not reach its root.");
            }

            int grandparent = parent[ancestor];

            int sibling = left[grandparent];
            if (sibling == ancestor)
            {
                sibling = right[grandparent];
                right[grandparent] = (ushort)current;
            }
            else
            {
                left[grandparent] = (ushort)current;
            }

            if (left[ancestor] == current)
            {
                left[ancestor] = (ushort)sibling;
            }
            else
            {
                right[ancestor] = (ushort)sibling;
            }

            parent[current] = (byte)grandparent;
            parent[sibling] = (byte)ancestor;

            current = grandparent;
            ancestor = parent[current];
        }
    }
}
