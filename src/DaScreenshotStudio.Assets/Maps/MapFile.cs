using System.Buffers.Binary;

namespace DaScreenshotStudio.Assets.Maps;

public sealed class MapFile
{
    private readonly byte[] _cells;

    private MapFile(byte[] cells, int width, int height)
    {
        _cells = cells;
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public MapCell this[int x, int y] => GetCell(x, y);

    public static MapFile Parse(ReadOnlySpan<byte> data, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var expected = (long)width * height * MapCell.Length;
        if (data.Length < expected)
        {
            throw new InvalidDataException(
                $"A {width} by {height} map needs {expected} bytes; the file holds {data.Length}.");
        }

        return new MapFile(data[..(int)expected].ToArray(), width, height);
    }

    public ReadOnlySpan<byte> GetRowBytes(int y) =>
        _cells.AsSpan(y * Width * MapCell.Length, Width * MapCell.Length);

    public MapCell GetCell(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return ReadCell(_cells.AsSpan(((y * Width) + x) * MapCell.Length));
    }

    public static ushort ReadGroundTile(ReadOnlySpan<byte> cell) => BinaryPrimitives.ReadUInt16LittleEndian(cell);

    public static MapCell ReadCell(ReadOnlySpan<byte> cell) => new(
        BinaryPrimitives.ReadUInt16LittleEndian(cell),
        BinaryPrimitives.ReadUInt16LittleEndian(cell[2..]),
        BinaryPrimitives.ReadUInt16LittleEndian(cell[4..]));
}
