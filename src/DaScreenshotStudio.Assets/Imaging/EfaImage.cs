using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Imaging;

public readonly struct EfaFrame : IEquatable<EfaFrame>
{
    public const int RecordLength = 0x40;

    internal EfaFrame(
        uint unknown00,
        int dataOffset,
        int compressedSize,
        int decodedSize,
        uint planeKind,
        int pixelOffset,
        int pixelPitch,
        uint pixelMode,
        int secondOffset,
        int secondPitch,
        ushort anchorX,
        ushort anchorY,
        short left,
        short top,
        short right,
        short bottom,
        short cropLeft,
        short cropTop,
        short cropRight,
        short cropBottom)
    {
        Unknown00 = unknown00;
        DataOffset = dataOffset;
        CompressedSize = compressedSize;
        DecodedSize = decodedSize;
        PlaneKind = planeKind;
        PixelOffset = pixelOffset;
        PixelPitch = pixelPitch;
        PixelMode = pixelMode;
        SecondOffset = secondOffset;
        SecondPitch = secondPitch;
        AnchorX = anchorX;
        AnchorY = anchorY;
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
        CropLeft = cropLeft;
        CropTop = cropTop;
        CropRight = cropRight;
        CropBottom = cropBottom;
    }

    public uint Unknown00 { get; }

    public int DataOffset { get; }

    public int CompressedSize { get; }

    public int DecodedSize { get; }

    public uint PlaneKind { get; }

    public int PixelOffset { get; }

    public int PixelPitch { get; }

    public uint PixelMode { get; }

    public int SecondOffset { get; }

    public int SecondPitch { get; }

    public ushort AnchorX { get; }

    public ushort AnchorY { get; }

    public short Left { get; }

    public short Top { get; }

    public short Right { get; }

    public short Bottom { get; }

    public short CropLeft { get; }

    public short CropTop { get; }

    public short CropRight { get; }

    public short CropBottom { get; }

    public int CropWidth => CropRight - CropLeft;

    public int CropHeight => CropBottom - CropTop;

    public bool Equals(EfaFrame other) =>
        DataOffset == other.DataOffset && CompressedSize == other.CompressedSize
        && DecodedSize == other.DecodedSize && PlaneKind == other.PlaneKind
        && PixelOffset == other.PixelOffset && PixelPitch == other.PixelPitch
        && PixelMode == other.PixelMode && SecondOffset == other.SecondOffset
        && SecondPitch == other.SecondPitch && Left == other.Left && Top == other.Top
        && Right == other.Right && Bottom == other.Bottom;

    public override bool Equals(object? obj) => obj is EfaFrame other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(DataOffset, CompressedSize, DecodedSize, PixelPitch, Left, Top, Right, Bottom);

    public static bool operator ==(EfaFrame left, EfaFrame right) => left.Equals(right);

    public static bool operator !=(EfaFrame left, EfaFrame right) => !left.Equals(right);
}

public sealed class EfaImage
{
    public const int HeaderLength = 0x40;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly EfaFrame[] _frames;
    private readonly int _payloadBase;

    private EfaImage(ReadOnlyMemory<byte> data, uint signature, uint frameIntervalMs, byte blendMode, uint flags, EfaFrame[] frames, int payloadBase)
    {
        _data = data;
        _frames = frames;
        _payloadBase = payloadBase;
        Signature = signature;
        FrameIntervalMs = frameIntervalMs;
        BlendMode = blendMode;
        Flags = flags;
    }

    public uint Signature { get; }

    public uint FrameIntervalMs { get; }

    public uint Flags { get; }

    public byte BlendMode { get; }

    public int FrameCount => _frames.Length;

    public EfaFrame this[int frameIndex] => _frames[frameIndex];

    public static EfaImage Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException($"An EFA needs {HeaderLength} header bytes; got {bytes.Length}.");
        }

        var signature = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var declaredFrames = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        var interval = BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        var blendMode = bytes[0x0C];
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x10..]);

        var available = (bytes.Length - HeaderLength) / EfaFrame.RecordLength;
        if (declaredFrames > (uint)Math.Max(available, 0))
        {
            throw new InvalidDataException($"An EFA declares {declaredFrames} frames, which does not fit its {bytes.Length} bytes.");
        }

        var frames = new EfaFrame[declaredFrames];
        for (var i = 0; i < frames.Length; i++)
        {
            var record = bytes.Slice(HeaderLength + (i * EfaFrame.RecordLength), EfaFrame.RecordLength);

            var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(record[0x08..]);
            var decodedSize = BinaryPrimitives.ReadUInt32LittleEndian(record[0x0C..]);
            if (compressedSize > int.MaxValue || decodedSize > int.MaxValue)
            {
                throw new InvalidDataException(
                    $"An EFA frame {i} declares {compressedSize} compressed bytes and {decodedSize} decoded, which is not a size a frame can have.");
            }

            frames[i] = new EfaFrame(
                BinaryPrimitives.ReadUInt32LittleEndian(record),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[0x04..]),
                (int)compressedSize,
                (int)decodedSize,
                BinaryPrimitives.ReadUInt32LittleEndian(record[0x10..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[0x14..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[0x18..]),
                BinaryPrimitives.ReadUInt32LittleEndian(record[0x1C..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[0x20..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(record[0x24..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[0x28..]),
                BinaryPrimitives.ReadUInt16LittleEndian(record[0x2A..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x2C..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x2E..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x30..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x32..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x34..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x36..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x38..]),
                BinaryPrimitives.ReadInt16LittleEndian(record[0x3A..]));
        }

        var payloadBase = HeaderLength + (frames.Length * EfaFrame.RecordLength);
        return new EfaImage(data, signature, interval, blendMode, flags, frames, payloadBase);
    }

    public ReadOnlySpan<byte> GetCompressed(int frameIndex)
    {
        var frame = _frames[frameIndex];
        var start = _payloadBase + frame.DataOffset;
        if (frame.CompressedSize <= 0 || start < 0 || (long)start + frame.CompressedSize > _data.Length)
        {
            return default;
        }

        return _data.Span.Slice(start, frame.CompressedSize);
    }

    public int Decode(int frameIndex, Span<byte> destination)
    {
        var frame = _frames[frameIndex];
        if (destination.Length < frame.DecodedSize)
        {
            throw new ArgumentException(
                $"Frame {frameIndex} inflates to {frame.DecodedSize} bytes, more than the {destination.Length} given.",
                nameof(destination));
        }

        return ZlibPayload.Inflate(GetCompressed(frameIndex), destination, frame.DecodedSize);
    }

    public static ReadOnlySpan<byte> GetPrimaryPlane(scoped in EfaFrame frame, ReadOnlySpan<byte> decoded)
    {
        var offset = frame.PixelOffset;
        var rows = frame.CropHeight;
        if (offset < 0 || frame.PixelPitch <= 0 || rows <= 0)
        {
            return default;
        }

        var length = (long)frame.PixelPitch * rows;
        return offset + length > decoded.Length ? default : decoded.Slice(offset, (int)length);
    }

    public static ReadOnlySpan<byte> GetSecondPlane(scoped in EfaFrame frame, ReadOnlySpan<byte> decoded)
    {
        var offset = frame.SecondOffset;
        return offset <= 0 || offset >= decoded.Length ? default : decoded[offset..];
    }

    public const int MaxAlphaWeight = 32;

    public static bool TryGetAlphaWeights(scoped in EfaFrame frame, ReadOnlySpan<byte> decoded, Span<byte> weights)
    {
        var width = frame.CropWidth;
        var height = frame.CropHeight;
        if (width <= 0 || height <= 0 || weights.Length < width * height)
        {
            return false;
        }

        var plane = GetSecondPlane(in frame, decoded);
        var table = height * 4;
        if (plane.Length < table)
        {
            return false;
        }

        for (var y = 0; y < height; y++)
        {
            var start = BinaryPrimitives.ReadUInt32LittleEndian(plane[(y * 4)..]);
            if (start < (uint)table || start > (uint)plane.Length)
            {
                return false;
            }

            var row = weights.Slice(y * width, width);
            var position = (int)start;
            var run = 0;
            byte weight = 0;

            for (var x = 0; x < width; x++)
            {
                while (run == 0)
                {
                    if (position + 2 > plane.Length)
                    {
                        return false;
                    }

                    var word = BinaryPrimitives.ReadUInt16LittleEndian(plane[position..]);
                    position += 2;

                    weight = (byte)Math.Min(word & 0xFF, MaxAlphaWeight);
                    run = word >> 8;
                }

                row[x] = weight;
                run--;
            }
        }

        return true;
    }
}
