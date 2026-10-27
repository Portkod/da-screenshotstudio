using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public readonly struct EpfFrame : IEquatable<EpfFrame>
{
    internal EpfFrame(short top, short left, short bottom, short right, int dataOffsetA, int dataOffsetB)
    {
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;
        DataOffsetA = dataOffsetA;
        DataOffsetB = dataOffsetB;
    }

    public short Top { get; }

    public short Left { get; }

    public short Bottom { get; }

    public short Right { get; }

    public int DataOffsetA { get; }

    public int DataOffsetB { get; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public long PixelCount => IsEmpty ? 0 : (long)Width * Height;

    public bool Equals(EpfFrame other) =>
        Top == other.Top && Left == other.Left && Bottom == other.Bottom && Right == other.Right
        && DataOffsetA == other.DataOffsetA && DataOffsetB == other.DataOffsetB;

    public override bool Equals(object? obj) => obj is EpfFrame other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Top, Left, Bottom, Right, DataOffsetA, DataOffsetB);

    public static bool operator ==(EpfFrame left, EpfFrame right) => left.Equals(right);

    public static bool operator !=(EpfFrame left, EpfFrame right) => !left.Equals(right);
}

public sealed class EpfImage
{
    public const int HeaderLength = 0x0C;

    public const int FrameRecordLength = 0x10;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly EpfFrame[] _frames;
    private readonly int[] _planes;

    private EpfImage(
        ReadOnlyMemory<byte> data,
        int width,
        int height,
        EpfFrame[] frames,
        int[] planes,
        bool hasTerminalBoundary)
    {
        _data = data;
        _frames = frames;
        _planes = planes;
        Width = width;
        Height = height;
        HasTerminalBoundary = hasTerminalBoundary;
    }

    public int Width { get; }

    public int Height { get; }

    public int FrameCount => _frames.Length;

    public bool HasTerminalBoundary { get; }

    public EpfFrame this[int frameIndex] => _frames[frameIndex];

    public static EpfImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException($"An EPF needs {HeaderLength} header bytes; got {bytes.Length}.");
        }

        int declaredFrames = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        int width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]);
        var displacement = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..]);

        var tableStart = HeaderLength + displacement;
        if (displacement < 0 || tableStart > bytes.Length)
        {
            throw new InvalidDataException($"An EPF frame table at {tableStart} lies outside its {bytes.Length} byte entry.");
        }

        var storedRecords = (bytes.Length - tableStart) / FrameRecordLength;
        var frameCount = Math.Min(declaredFrames, storedRecords);

        var frames = new EpfFrame[frameCount];
        var planes = new int[frameCount * 4];

        for (var i = 0; i < frameCount; i++)
        {
            var record = bytes.Slice(tableStart + (i * FrameRecordLength), FrameRecordLength);
            var frame = new EpfFrame(
                BinaryPrimitives.ReadInt16LittleEndian(record),
                BinaryPrimitives.ReadInt16LittleEndian(record[2..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[4..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[6..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(record[12..]));

            frames[i] = frame;

            Clamp(HeaderLength + frame.DataOffsetA, frame.PixelCount, bytes.Length, out var pixelStart, out var pixelLength);

            var nextRecord = tableStart + ((i + 1) * FrameRecordLength);
            var secondSize = 0;
            if (nextRecord + FrameRecordLength <= bytes.Length)
            {
                secondSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(nextRecord + 8, 4)) - frame.DataOffsetB;
            }

            Clamp(HeaderLength + frame.DataOffsetB, secondSize, bytes.Length, out var secondStart, out var secondLength);

            var slot = i * 4;
            planes[slot] = pixelStart;
            planes[slot + 1] = pixelLength;
            planes[slot + 2] = secondStart;
            planes[slot + 3] = secondLength;
        }

        var hasTerminal = bytes.Length - tableStart >= (frameCount + 1) * FrameRecordLength;

        return new EpfImage(data, width, height, frames, planes, hasTerminal);
    }

    public ReadOnlySpan<byte> GetPixels(int frameIndex)
    {
        var slot = frameIndex * 4;
        return _data.Span.Slice(_planes[slot], _planes[slot + 1]);
    }

    private static void Clamp(int start, long length, int limit, out int clampedStart, out int clampedLength)
    {
        if (start < 0 || start > limit || length <= 0)
        {
            clampedStart = 0;
            clampedLength = 0;
            return;
        }

        clampedStart = start;
        clampedLength = (int)Math.Min(length, limit - start);
    }
}
