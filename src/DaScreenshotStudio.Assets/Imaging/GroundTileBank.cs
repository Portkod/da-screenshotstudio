namespace DaScreenshotStudio.Assets.Imaging;

public sealed class GroundTileBank
{
    public const int TileWidth = 56;

    public const int TileHeight = 27;

    public const int RecordLength = TileWidth * TileHeight;

    public const int VisiblePixelCount = 784;

    private readonly ReadOnlyMemory<byte> _data;

    private GroundTileBank(ReadOnlyMemory<byte> data, int count)
    {
        _data = data;
        Count = count;
    }

    public int Count { get; }

    public static GroundTileBank Wrap(ReadOnlyMemory<byte> data)
    {
        var count = data.Length / RecordLength;
        return new GroundTileBank(data[..(count * RecordLength)], count);
    }

    public bool ContainsRecord(int recordIndex) => (uint)recordIndex < (uint)Count;

    public ReadOnlySpan<byte> GetRecord(int recordIndex) =>
        _data.Span.Slice(recordIndex * RecordLength, RecordLength);

    public static int RowLeft(int row) => Math.Abs(13 - row) * 2;

    public static int RowVisibleWidth(int row) => TileWidth - (RowLeft(row) * 2);

    public int CopyVisiblePixels(int recordIndex, Span<byte> destination)
    {
        if (destination.Length < VisiblePixelCount)
        {
            throw new ArgumentException($"A tile has {VisiblePixelCount} visible pixels.", nameof(destination));
        }

        var record = GetRecord(recordIndex);
        var written = 0;

        for (var row = 0; row < TileHeight; row++)
        {
            var left = RowLeft(row);
            var width = RowVisibleWidth(row);
            record.Slice((row * TileWidth) + left, width).CopyTo(destination[written..]);
            written += width;
        }

        return written;
    }
}
