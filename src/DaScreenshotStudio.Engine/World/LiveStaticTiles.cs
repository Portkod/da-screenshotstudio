using DaScreenshotStudio.Assets.Maps;

namespace DaScreenshotStudio.Engine.World;

public sealed class LiveStaticTiles
{
    private ushort[] _tiles = [];
    private int _width;
    private int _height;

    public int AppliedCount { get; private set; }

    public int Width => _width;

    public int Height => _height;

    public void Reset(MapFile cells)
    {
        ArgumentNullException.ThrowIfNull(cells);

        _width = cells.Width;
        _height = cells.Height;
        AppliedCount = 0;

        var slots = _width * _height * 2;
        if (_tiles.Length < slots)
        {
            _tiles = new ushort[slots];
        }

        for (var y = 0; y < _height; y++)
        {
            var row = cells.GetRowBytes(y);

            for (var x = 0; x < _width; x++)
            {
                var cell = MapFile.ReadCell(row[(x * MapCell.Length)..]);
                var slot = (((y * _width) + x) * 2);
                _tiles[slot] = cell.LeftStaticTile;
                _tiles[slot + 1] = cell.RightStaticTile;
            }
        }
    }

    public void Clear()
    {
        _width = 0;
        _height = 0;
        AppliedCount = 0;
    }

    public bool Covers(int width, int height) => _width == width && _height == height && _width > 0;

    public ushort Left(int x, int y) => _tiles[((y * _width) + x) * 2];

    public ushort Right(int x, int y) => _tiles[(((y * _width) + x) * 2) + 1];

    public bool Apply(int x, int y, bool leftSide, byte state)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
        {
            return false;
        }

        var slot = ((((y * _width) + x) * 2)) + (leftSide ? 0 : 1);
        var current = _tiles[slot];

        if (!StaticStatePairs.TryApplyState(current, state, out var tile) || tile == current)
        {
            return false;
        }

        _tiles[slot] = tile;
        AppliedCount++;
        return true;
    }
}
