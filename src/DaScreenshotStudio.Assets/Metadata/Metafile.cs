using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace DaScreenshotStudio.Assets.Metadata;

public sealed class Metafile
{
    public const int MaxDecodedLength = 0x20000;

    private readonly byte[] _decoded;
    private readonly string[] _groupNames;
    private readonly int[] _groupValueStarts;
    private readonly int[] _valueStarts;
    private readonly int[] _valueLengths;
    private readonly Dictionary<string, int> _byName;
    private readonly Dictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    private Metafile(
        string name,
        byte[] decoded,
        string[] groupNames,
        int[] groupValueStarts,
        int[] valueStarts,
        int[] valueLengths)
    {
        Name = name;
        _decoded = decoded;
        _groupNames = groupNames;
        _groupValueStarts = groupValueStarts;
        _valueStarts = valueStarts;
        _valueLengths = valueLengths;

        _byName = new Dictionary<string, int>(groupNames.Length, StringComparer.Ordinal);
        for (var i = 0; i < groupNames.Length; i++)
        {
            _byName[groupNames[i]] = i;
        }

        _lookup = _byName.GetAlternateLookup<ReadOnlySpan<char>>();
    }

    public string Name { get; }

    public int GroupCount => _groupNames.Length;

    public static Metafile Parse(string name, ReadOnlySpan<byte> compressed)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return ParseDecoded(name, Inflate(compressed, name));
    }

    public static Metafile ParseDecoded(string name, byte[] decoded)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(decoded);

        var bytes = (ReadOnlySpan<byte>)decoded;
        if (bytes.Length < 2)
        {
            throw new InvalidDataException($"Metadata table '{name}' is {bytes.Length} bytes, too short for a group count.");
        }

        var groupCount = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        var position = 2;

        if (groupCount * 3 > bytes.Length - 2)
        {
            throw new InvalidDataException(
                $"Metadata table '{name}' declares {groupCount} groups over {bytes.Length - 2} remaining bytes.");
        }

        var groupNames = new string[groupCount];
        var groupValueStarts = new int[groupCount + 1];
        var valueStarts = new List<int>();
        var valueLengths = new List<int>();

        for (var group = 0; group < groupCount; group++)
        {
            if (position >= bytes.Length)
            {
                throw new InvalidDataException($"Metadata table '{name}' ends inside group {group}.");
            }

            int nameLength = bytes[position++];
            if (position + nameLength + 2 > bytes.Length)
            {
                throw new InvalidDataException($"Metadata table '{name}' ends inside the name of group {group}.");
            }

            groupNames[group] = Encoding.Latin1.GetString(bytes.Slice(position, nameLength));
            position += nameLength;

            var valueCount = BinaryPrimitives.ReadUInt16BigEndian(bytes[position..]);
            position += 2;

            groupValueStarts[group] = valueStarts.Count;

            for (var value = 0; value < valueCount; value++)
            {
                if (position + 2 > bytes.Length)
                {
                    throw new InvalidDataException($"Metadata table '{name}' ends inside group '{groupNames[group]}'.");
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes[position..]);
                position += 2;

                if (position + length > bytes.Length)
                {
                    throw new InvalidDataException($"Metadata table '{name}' declares a value past its end.");
                }

                valueStarts.Add(position);
                valueLengths.Add(length);
                position += length;
            }
        }

        groupValueStarts[groupCount] = valueStarts.Count;

        return new Metafile(name, decoded, groupNames, groupValueStarts, [.. valueStarts], [.. valueLengths]);
    }

    public string GetGroupName(int group) => _groupNames[group];

    public int GetValueCount(int group) => _groupValueStarts[group + 1] - _groupValueStarts[group];

    public ReadOnlySpan<byte> GetValue(int group, int value)
    {
        var slot = _groupValueStarts[group] + value;
        if (slot >= _groupValueStarts[group + 1])
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return _decoded.AsSpan(_valueStarts[slot], _valueLengths[slot]);
    }

    public bool TryFindGroup(scoped ReadOnlySpan<char> name, out int group) => _lookup.TryGetValue(name, out group);

    private static byte[] Inflate(ReadOnlySpan<byte> compressed, string name)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(compressed.Length);
        try
        {
            compressed.CopyTo(buffer);

            using var source = new MemoryStream(buffer, 0, compressed.Length, writable: false);
            using var inflate = new ZLibStream(source, CompressionMode.Decompress);
            const int limit = MaxDecodedLength + 1;
            var decoded = ArrayPool<byte>.Shared.Rent(limit);
            var filled = 0;

            try
            {
                while (filled < limit)
                {
                    var read = inflate.Read(decoded, filled, limit - filled);
                    if (read == 0)
                    {
                        break;
                    }

                    filled += read;
                }

                if (filled > MaxDecodedLength)
                {
                    throw new InvalidDataException(
                        $"Metadata table '{name}' inflates past the client's {MaxDecodedLength} byte buffer.");
                }

                return decoded[..filled];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(decoded);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
