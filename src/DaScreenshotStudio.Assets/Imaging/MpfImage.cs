using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public readonly struct MpfFrame : IEquatable<MpfFrame>
{
    public const int RecordLength = 0x10;

    public const int MarkerCoordinate = 0xFFFF;

    internal MpfFrame(int left, int top, int right, int bottom, int anchorX, int anchorY, int dataOffset)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        AnchorX = anchorX;
        AnchorY = anchorY;
        DataOffset = dataOffset;
    }

    public int Left { get; }

    public int Top { get; }

    public int Right { get; }

    public int Bottom { get; }

    public int AnchorX { get; }

    public int AnchorY { get; }

    public int DataOffset { get; }

    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool IsPaletteMarker =>
        Left == MarkerCoordinate && Top == MarkerCoordinate
        && Right == MarkerCoordinate && Bottom == MarkerCoordinate;

    public bool IsDrawable => !IsPaletteMarker && Width > 0 && Height > 0;

    public long PixelCount => IsDrawable ? (long)Width * Height : 0;

    public bool Equals(MpfFrame other) =>
        Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom
        && AnchorX == other.AnchorX && AnchorY == other.AnchorY && DataOffset == other.DataOffset;

    public override bool Equals(object? obj) => obj is MpfFrame other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Left, Top, Right, Bottom, AnchorX, AnchorY, DataOffset);

    public static bool operator ==(MpfFrame left, MpfFrame right) => left.Equals(right);

    public static bool operator !=(MpfFrame left, MpfFrame right) => !left.Equals(right);
}

public readonly struct MpfStandingSegment
{
    public MpfStandingSegment(int chance, int frameDelayMs, int startFrame, int steps, int viewStride)
    {
        Chance = chance;
        FrameDelayMs = frameDelayMs;
        StartFrame = startFrame;
        Steps = steps;
        ViewStride = viewStride;
    }

    public int Chance { get; }

    public int FrameDelayMs { get; }

    public int StartFrame { get; }

    public int Steps { get; }

    public int ViewStride { get; }
}

public readonly struct MpfColoringRange : IEquatable<MpfColoringRange>
{
    public MpfColoringRange(byte table, byte firstIndex, byte lastIndex)
    {
        Table = table;
        FirstIndex = firstIndex;
        LastIndex = lastIndex;
    }

    public byte Table { get; }

    public byte FirstIndex { get; }

    public byte LastIndex { get; }

    public bool Equals(MpfColoringRange other) =>
        Table == other.Table && FirstIndex == other.FirstIndex && LastIndex == other.LastIndex;

    public override bool Equals(object? obj) => obj is MpfColoringRange other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Table, FirstIndex, LastIndex);

    public static bool operator ==(MpfColoringRange left, MpfColoringRange right) => left.Equals(right);

    public static bool operator !=(MpfColoringRange left, MpfColoringRange right) => !left.Equals(right);
}

public sealed class MpfImage
{
    public const int HeaderLength = 17;

    public const int AttackRangeCount = 3;

    public const int StoredViewCount = 2;

    private const byte ExtendedFormMarker = 0xFF;

    private const uint EnvelopeMarker = 0xFFFFFFFF;

    private const uint EnvelopeExtraBlockFlag = 4;

    private const uint EnvelopeDyeFlag = 1;

    public const int MaxColoringRanges = 4;

    public const int MinWalkSequence = 1;

    public const int MaxWalkSequence = 8;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly MpfFrame[] _frames;
    private readonly byte[] _attacks;
    private readonly MpfStandingSegment[] _standing;
    private readonly MpfColoringRange[] _coloringRanges;
    private readonly int _pixelBlockStart;

    private MpfImage(
        ReadOnlyMemory<byte> data,
        MpfFrame[] frames,
        byte[] attacks,
        int pixelBlockStart,
        int width,
        int height,
        uint declaredPixelBlockLength,
        int walkStart,
        int walkLength,
        bool isExtendedForm,
        bool hasEnvelope,
        uint envelopeFlags,
        int envelopeRecordCount,
        MpfColoringRange[] coloringRanges,
        byte idleStart,
        byte idleLength,
        byte idleSecondaryLength,
        byte idleDelayFactor,
        int paletteNumber,
        bool hasPaletteMarker)
    {
        _data = data;
        _frames = frames;
        _attacks = attacks;
        _pixelBlockStart = pixelBlockStart;
        Width = width;
        Height = height;
        DeclaredPixelBlockLength = declaredPixelBlockLength;
        WalkStart = walkStart;
        WalkLength = walkLength;
        IsExtendedForm = isExtendedForm;
        HasEnvelope = hasEnvelope;
        EnvelopeFlags = envelopeFlags;
        EnvelopeRecordCount = envelopeRecordCount;
        _coloringRanges = coloringRanges;
        IdleStart = idleStart;
        IdleLength = idleLength;
        IdleSecondaryLength = idleSecondaryLength;
        IdleDelayFactor = idleDelayFactor;
        PaletteNumber = paletteNumber;
        HasPaletteMarker = hasPaletteMarker;
        _standing = BuildStandingSegments(walkStart, walkLength, idleStart, idleLength, idleSecondaryLength, idleDelayFactor);
    }

    public int Width { get; }

    public int Height { get; }

    public uint DeclaredPixelBlockLength { get; }

    public int WalkStart { get; }

    public int WalkLength { get; }

    public bool IsExtendedForm { get; }

    public bool HasEnvelope { get; }

    public uint EnvelopeFlags { get; }

    public bool AdmitsDye => (EnvelopeFlags & EnvelopeDyeFlag) != 0;

    public ReadOnlySpan<MpfColoringRange> ColoringRanges => _coloringRanges;

    public int EnvelopeRecordCount { get; }

    public byte IdleStart { get; }

    public byte IdleLength { get; }

    public byte IdleSecondaryLength { get; }

    public byte IdleDelayFactor { get; }

    public ReadOnlySpan<MpfStandingSegment> StandingSegments => _standing;

    public int WalkSequence =>
        WalkLength is >= MinWalkSequence and <= MaxWalkSequence ? WalkLength : 0;

    public int PickStandingSegment(int roll)
    {
        for (var i = 0; i < _standing.Length; i++)
        {
            if (roll < _standing[i].Chance && _standing[i].Chance >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    public int SelectStandingFrame(in MpfStandingSegment segment, int step, int group)
    {
        if (segment.Steps <= 0 || (uint)step >= (uint)segment.Steps || group < 0)
        {
            return -1;
        }

        var frame = segment.StartFrame + step;
        if (group > 0 && frame + segment.ViewStride < FrameCount)
        {
            frame += segment.ViewStride;
        }

        return (uint)frame < (uint)PictureCount && _frames[frame].IsDrawable ? frame : -1;
    }

    private static MpfStandingSegment[] BuildStandingSegments(
        int walkStart,
        int walkLength,
        byte idleStart,
        byte idleLength,
        byte idleSecondaryLength,
        byte idleDelayFactor)
    {
        if (idleLength < 1)
        {
            return [new MpfStandingSegment(100, 10000, walkStart, 1, walkLength)];
        }

        if (idleLength != idleSecondaryLength && idleSecondaryLength >= 1)
        {
            return
            [
                new MpfStandingSegment(idleDelayFactor, 300, idleStart, idleLength, idleLength),
                new MpfStandingSegment(100, 300, idleStart, idleSecondaryLength, idleLength),
            ];
        }

        if (idleDelayFactor == 0)
        {
            return [];
        }

        return [new MpfStandingSegment(100, Math.Max(idleDelayFactor * 100, 100), idleStart, idleLength, idleLength)];
    }

    public int PaletteNumber { get; }

    public bool HasPaletteMarker { get; }

    public int FrameCount => _frames.Length;

    public int PictureCount => HasPaletteMarker ? _frames.Length - 1 : _frames.Length;

    public ReadOnlySpan<MpfFrame> Frames => _frames;

    public ReadOnlySpan<byte> AttackLengths => _attacks.AsSpan(AttackRangeCount, AttackRangeCount);

    public MpfFrame this[int frameIndex] => _frames[frameIndex];

    public static string NameFor(int sprite) => $"mns{sprite:D3}.mpf";

    public static string PaletteNameFor(int paletteNumber) => $"mns{paletteNumber:D3}.pal";

    public static MpfImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        var position = 0;

        var hasEnvelope = false;
        uint flags = 0;
        var envelopeRecords = 0;
        var coloringRanges = Array.Empty<MpfColoringRange>();

        if (bytes.Length >= 8 && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == EnvelopeMarker)
        {
            hasEnvelope = true;
            flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
            position = 8;

            if ((flags & EnvelopeExtraBlockFlag) != 0)
            {
                Require(bytes, position, 4);
                var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]);
                if (count > int.MaxValue / 4)
                {
                    throw new InvalidDataException($"An MPF envelope declares {count} extra records.");
                }

                envelopeRecords = (int)count;
                position += 4;
                Require(bytes, position, envelopeRecords * 4);

                var kept = Math.Min(envelopeRecords, MaxColoringRanges);
                if (kept > 0)
                {
                    coloringRanges = new MpfColoringRange[kept];
                    for (var i = 0; i < kept; i++)
                    {
                        var record = bytes.Slice(position + (i * 4), 4);
                        coloringRanges[i] = new MpfColoringRange(record[0], record[1], record[2]);
                    }
                }

                position += envelopeRecords * 4;
            }
        }

        Require(bytes, position, HeaderLength);
        var header = bytes.Slice(position, HeaderLength);

        int frameCount = header[0];
        int width = BinaryPrimitives.ReadUInt16LittleEndian(header[1..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(header[3..]);
        var pixelBlockLength = BinaryPrimitives.ReadUInt32LittleEndian(header[5..]);
        int walkStart = header[9];
        int walkLength = header[10];
        var extended = header[11] == ExtendedFormMarker;
        position += HeaderLength;

        var attacks = new byte[AttackRangeCount * 2];
        if (extended)
        {
            Require(bytes, position, AttackRangeCount * 2);
            for (var i = 0; i < AttackRangeCount; i++)
            {
                attacks[i] = bytes[position + (i * 2)];
                attacks[AttackRangeCount + i] = bytes[position + (i * 2) + 1];
            }

            position += AttackRangeCount * 2;
        }
        else
        {
            attacks.AsSpan(0, AttackRangeCount).Fill(header[11]);
            attacks.AsSpan(AttackRangeCount, AttackRangeCount).Fill(header[12]);
        }

        var tableStart = position;
        Require(bytes, tableStart, frameCount * MpfFrame.RecordLength);

        var frames = new MpfFrame[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            var record = bytes.Slice(tableStart + (i * MpfFrame.RecordLength), MpfFrame.RecordLength);
            frames[i] = new MpfFrame(
                BinaryPrimitives.ReadUInt16LittleEndian(record),
                BinaryPrimitives.ReadUInt16LittleEndian(record[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[6..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[10..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[12..]));
        }

        position = tableStart + (frameCount * MpfFrame.RecordLength);

        var hasMarker = frameCount > 0 && frames[^1].IsPaletteMarker;
        var palette = hasMarker ? frames[^1].DataOffset : 0;

        return new MpfImage(
            data,
            frames,
            attacks,
            position,
            width,
            height,
            pixelBlockLength,
            walkStart,
            walkLength,
            extended,
            hasEnvelope,
            flags,
            envelopeRecords,
            coloringRanges,
            header[13],
            header[15],
            header[14],
            header[16],
            palette,
            hasMarker);
    }

    public int SelectWalkFrame(int group, int motionFrame) =>
        SelectRangeFrame(WalkStart, WalkLength, group, motionFrame);

    public int SelectAttackFrame(int attack, int group, int motionFrame)
    {
        if ((uint)attack >= AttackRangeCount)
        {
            return -1;
        }

        return SelectRangeFrame(_attacks[attack], _attacks[AttackRangeCount + attack], group, motionFrame);
    }

    private int SelectRangeFrame(int start, int length, int group, int motionFrame)
    {
        var pictures = PictureCount;
        if (pictures == 0 || group < 0)
        {
            return -1;
        }

        var span = Math.Max(1, length);
        var offset = Math.Clamp(motionFrame, 0, span - 1);

        for (var view = Math.Min(group, StoredViewCount - 1); view >= 0; view--)
        {
            var frame = start + (view * span) + offset;
            if ((uint)frame < (uint)pictures && _frames[frame].IsDrawable)
            {
                return frame;
            }
        }

        return -1;
    }

    public ReadOnlySpan<byte> GetPixels(int frameIndex)
    {
        if ((uint)frameIndex >= (uint)_frames.Length)
        {
            return default;
        }

        var frame = _frames[frameIndex];
        var length = frame.PixelCount;
        if (length == 0 || frame.DataOffset < 0)
        {
            return default;
        }

        var start = (long)_pixelBlockStart + frame.DataOffset;
        return start + length > _data.Length ? default : _data.Span.Slice((int)start, (int)length);
    }

    private static void Require(ReadOnlySpan<byte> data, int position, int size)
    {
        if (position < 0 || size < 0 || size > data.Length - position)
        {
            throw new InvalidDataException(
                $"An MPF needs {size} bytes at {position}; the entry holds {data.Length}.");
        }
    }
}
