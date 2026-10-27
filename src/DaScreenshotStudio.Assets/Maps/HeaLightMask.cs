using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Maps;

public sealed class HeaLightMask
{
    public const int HeaderLength = 0x28;

    public const int TileHalfWidth = 28;

    public const int TileHalfHeight = 14;

    private readonly ReadOnlyMemory<byte> _data;
    private readonly int[] _bandStarts;
    private readonly int _rowTableStart;
    private readonly int _runStart;
    private readonly int _runCount;

    public int ByteLength => _data.Length;

    private HeaLightMask(
        ReadOnlyMemory<byte> data,
        uint unknown00,
        int screenWidth,
        int screenHeight,
        uint unknown0C,
        uint unknown10,
        int mapWidth,
        int mapHeight,
        int canvasWidth,
        int canvasHeight,
        int[] bandStarts,
        int rowTableStart,
        int runStart,
        int runCount)
    {
        _data = data;
        _bandStarts = bandStarts;
        _rowTableStart = rowTableStart;
        _runStart = runStart;
        _runCount = runCount;
        Unknown00 = unknown00;
        ScreenWidth = screenWidth;
        ScreenHeight = screenHeight;
        Unknown0C = unknown0C;
        Unknown10 = unknown10;
        MapWidth = mapWidth;
        MapHeight = mapHeight;
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
    }

    public uint Unknown00 { get; }

    public int ScreenWidth { get; }

    public int ScreenHeight { get; }

    public uint Unknown0C { get; }

    public uint Unknown10 { get; }

    public int MapWidth { get; }

    public int MapHeight { get; }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    public static HeaLightMask Parse(ReadOnlyMemory<byte> data)
    {
        var bytes = data.Span;
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException($"An HEA needs {HeaderLength} header bytes; got {bytes.Length}.");
        }

        var unknown00 = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        var screenWidth = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        var screenHeight = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]);
        var unknown0C = BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]);
        var unknown10 = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        var mapWidth = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);
        var mapHeight = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[24..]);
        var canvasWidth = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[28..]);
        var canvasHeight = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[32..]);
        var bandCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[36..]);

        if (bandCount <= 0 || canvasHeight <= 0)
        {
            throw new InvalidDataException($"An HEA declares {bandCount} bands over {canvasHeight} scanlines.");
        }

        var rowTableStart = HeaderLength + ((long)bandCount * 4);
        var runStart = rowTableStart + ((long)bandCount * canvasHeight * 4);
        if (rowTableStart > bytes.Length || runStart > bytes.Length)
        {
            throw new InvalidDataException(
                $"An HEA needs {runStart} bytes for its {bandCount} band row tables; the file holds {bytes.Length}.");
        }

        var bandStarts = new int[bandCount];
        for (var i = 0; i < bandCount; i++)
        {
            bandStarts[i] = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(HeaderLength + (i * 4), 4));
        }

        return new HeaLightMask(
            data,
            unknown00,
            screenWidth,
            screenHeight,
            unknown0C,
            unknown10,
            mapWidth,
            mapHeight,
            canvasWidth,
            canvasHeight,
            bandStarts,
            (int)rowTableStart,
            (int)runStart,
            (int)(bytes.Length - runStart) / 2);
    }

    public void Project(int axisA, int axisB, out int canvasX, out int canvasY)
    {
        canvasX = ((axisA - axisB + MapHeight) * TileHalfWidth) + ScreenWidth;
        canvasY = ((axisA + axisB) * TileHalfHeight) + ScreenHeight;
    }

    public int FindBand(int canvasX)
    {
        var band = 0;
        for (var i = 1; i < _bandStarts.Length; i++)
        {
            if (_bandStarts[i] > canvasX)
            {
                break;
            }

            band = i;
        }

        return band;
    }

    public int GetRowOffset(int band, int canvasY)
    {
        if ((uint)band >= (uint)_bandStarts.Length || (uint)canvasY >= (uint)CanvasHeight)
        {
            return -1;
        }

        var position = _rowTableStart + (((band * CanvasHeight) + canvasY) * 4);
        return (int)BinaryPrimitives.ReadUInt32LittleEndian(_data.Span.Slice(position, 4));
    }

    public int DecodeRow(int band, int canvasY, int startX, Span<byte> destination)
    {
        var offset = GetRowOffset(band, canvasY);
        if (offset < 0 || offset >= _runCount || destination.IsEmpty)
        {
            return 0;
        }

        var skip = startX - _bandStarts[band];
        if (skip < 0)
        {
            return 0;
        }

        var runs = _data.Span[_runStart..];

        var cursor = offset;
        while (cursor < _runCount)
        {
            var run = BinaryPrimitives.ReadUInt16LittleEndian(runs[(cursor * 2)..]);
            var length = run >> 8;
            if (length <= 0)
            {
                return 0;
            }

            if (skip < length)
            {
                break;
            }

            skip -= length;
            cursor++;
        }

        var written = 0;
        while (written < destination.Length && cursor < _runCount)
        {
            var run = BinaryPrimitives.ReadUInt16LittleEndian(runs[(cursor * 2)..]);
            var length = run >> 8;
            if (length <= 0)
            {
                break;
            }

            var intensity = (byte)run;
            var available = Math.Min(length - skip, destination.Length - written);
            destination.Slice(written, available).Fill(intensity);

            written += available;
            skip = 0;
            cursor++;
        }

        return written;
    }
}
