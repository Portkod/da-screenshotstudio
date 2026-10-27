using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Effects;

public sealed class EffectOffsetTable
{
    public const int RecordLength = 4;

    public static EffectOffsetTable Empty { get; } = new([]);

    private readonly byte[] _data;

    private EffectOffsetTable(byte[] data) => _data = data;

    public static EffectOffsetTable Parse(ReadOnlySpan<byte> data) =>
        data.IsEmpty ? Empty : new EffectOffsetTable(data.ToArray());

    public static string NameFor(int effectNumber) => $"efct{effectNumber:D3}.tbl";

    public static string MotionNameFor(int motionIndex) => $"mefc{motionIndex:D3}.tbl";

    public void Anchor(int frameIndex, out int x, out int y)
    {
        if (!TryAnchor(frameIndex, out x, out y))
        {
            x = 0;
            y = 0;
        }
    }

    public bool TryAnchor(int frameIndex, out int x, out int y)
    {
        var start = frameIndex * RecordLength;
        if (frameIndex < 0 || start + RecordLength > _data.Length)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(start));
        y = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(start + 2));
        return true;
    }
}
