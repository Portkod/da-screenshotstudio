using System.Buffers.Binary;
using System.Text;
using DaScreenshotStudio.Assets.Imaging;

namespace DaScreenshotStudio.Assets.Characters;

public readonly struct AlbumRecord : IEquatable<AlbumRecord>
{
    public const int RecordLength = 0x60;

    public const int CaptionLength = 0x4C;

    public const int CaptionOffset = 0x14;

    public const uint ActiveFlag = 1;

    private readonly byte[]? _captionBytes;
    private readonly byte[]? _payload;

    internal AlbumRecord(
        uint flags,
        int width,
        int height,
        int payloadOffset,
        int compressedLength,
        byte[]? captionBytes,
        byte[]? payload)
    {
        Flags = flags;
        Width = width;
        Height = height;
        PayloadOffset = payloadOffset;
        CompressedLength = compressedLength;
        _captionBytes = captionBytes;
        _payload = payload;
    }

    public uint Flags { get; }

    public int Width { get; }

    public int Height { get; }

    public int PayloadOffset { get; }

    public int CompressedLength { get; }

    public ReadOnlySpan<byte> CaptionBytes => _captionBytes ?? [];

    public string Caption
    {
        get
        {
            var bytes = CaptionBytes;
            var end = bytes.IndexOf((byte)0);
            return Encoding.Latin1.GetString(end < 0 ? bytes : bytes[..end]);
        }
    }

    public ReadOnlySpan<byte> Payload => _payload ?? [];

    public bool IsActive => (Flags & ActiveFlag) != 0;

    public int DecodedLength => Width * Height * 2;

    public bool Equals(AlbumRecord other) =>
        Flags == other.Flags && Width == other.Width && Height == other.Height
        && PayloadOffset == other.PayloadOffset && CompressedLength == other.CompressedLength
        && CaptionBytes.SequenceEqual(other.CaptionBytes);

    public override bool Equals(object? obj) => obj is AlbumRecord other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Flags, Width, Height, PayloadOffset, CompressedLength, Caption);

    public static bool operator ==(AlbumRecord left, AlbumRecord right) => left.Equals(right);

    public static bool operator !=(AlbumRecord left, AlbumRecord right) => !left.Equals(right);
}

public sealed class AlbumFile
{
    public const string FileName = "album.dat";

    public const int HeaderLength = 0x40;

    public const int DefaultCapacity = 100;

    private readonly AlbumRecord[] _records;

    private AlbumFile(AlbumRecord[] records, int capacity)
    {
        _records = records;
        Capacity = capacity;
    }

    public int Capacity { get; }

    public AlbumRecord this[int slot] => _records[slot];

    public static AlbumFile? TryLoad(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        return File.Exists(path) ? TryRead(File.ReadAllBytes(path)) : null;
    }

    public static AlbumFile? TryRead(scoped ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            return null;
        }

        var capacity = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (capacity is 0 or > DefaultCapacity)
        {
            return null;
        }

        var tableOffset = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        var payloadBase = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);

        var tableLength = (long)capacity * AlbumRecord.RecordLength;
        if (tableOffset > (uint)data.Length || tableOffset + tableLength > data.Length)
        {
            return null;
        }

        var records = new AlbumRecord[capacity];
        for (var i = 0; i < records.Length; i++)
        {
            var row = data.Slice((int)tableOffset + (i * AlbumRecord.RecordLength), AlbumRecord.RecordLength);

            var flags = BinaryPrimitives.ReadUInt32LittleEndian(row);
            var width = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            var height = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[8..]);
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[0x0C..]);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(row[0x10..]);

            if (width <= 0 || height <= 0 || (long)width * height * 2 > int.MaxValue)
            {
                flags &= ~AlbumRecord.ActiveFlag;
                width = 0;
                height = 0;
            }

            byte[]? payload = null;
            if ((flags & AlbumRecord.ActiveFlag) != 0 && length > 0)
            {
                var start = (long)payloadBase + offset;
                if (offset < 0 || start < 0 || start + length > data.Length)
                {
                    return null;
                }

                payload = data.Slice((int)start, length).ToArray();
            }

            records[i] = new AlbumRecord(
                flags,
                width,
                height,
                offset,
                length,
                row.Slice(AlbumRecord.CaptionOffset, AlbumRecord.CaptionLength).ToArray(),
                payload);
        }

        return new AlbumFile(records, (int)capacity);
    }

    public ReadOnlySpan<byte> GetCompressed(int slot) =>
        (uint)slot >= (uint)_records.Length ? default : _records[slot].Payload;

    public byte[] Decode(int slot) =>
        ZlibPayload.Inflate(GetCompressed(slot), _records[slot].DecodedLength);
}
