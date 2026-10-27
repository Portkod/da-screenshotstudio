using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Ui;
using DaScreenshotStudio.Engine.Core;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldMinimapPane : Pane
{
    public const int CellStride = 256;

    public const int CellCapacity = CellStride * CellStride;

    public const byte WallLevel = 1;

    public const byte LocalPlayerLevel = 180;

    public const byte RemoteHumanLevel = 100;

    public const int DefaultScale = 5;

    public const int ZoomingClassScale = 3;

    public const int ZoomingClass = 2;

    public const int MinimumScale = 1;

    public const int EmptyTileId = 10000;

    public const int ZoomInKey = 0x93;

    public const int ZoomOutKey = 0x94;

    private const byte WallPalette = 0x16;
    private const byte RemoteHumanPalette = 0x58;
    private const byte MonsterPalette120 = 0x28;
    private const byte MonsterPalette130 = 0xFF;
    private const byte MonsterPalette140 = 0x80;
    private const byte MonsterPalette150 = 0x28;
    private const byte LocalPlayerPalette = 0x45;

    private const byte WallEdgePalette = 0xFF;

    private readonly byte[] _level = new byte[CellCapacity];
    private readonly byte[] _staticLevel = new byte[CellCapacity];
    private readonly byte[] _objectLevel = new byte[CellCapacity];
    private readonly byte[] _refreshRequested = new byte[CellCapacity];
    private readonly byte[] _redrawPending = new byte[CellCapacity];
    private readonly byte[] _cellVisible = new byte[CellCapacity];

    private readonly int[] _requested = new int[CellCapacity];

    private readonly int[] _touched = new int[WorldObjectTable.Capacity];

    private int _requestedCount;
    private int _touchedCount;
    private readonly byte[] _levelPalette = new byte[256];
    private readonly ushort[] _colors = new ushort[256];

    private readonly WorldScene _scene;
    private readonly SotpTable _collision;

    private int _scale = DefaultScale;
    private int _centerX;
    private int _centerY;
    private int _mapWidth;
    private int _mapHeight;
    private MapFile? _cells;
    private bool _forceRefresh = true;
    private bool _forcedThisPass;

    private int _reindexes;

    public WorldMinimapPane(in PixelRect bounds, PixelBlender blender, WorldScene scene, SotpTable collision)
        : base(in bounds, blender)
    {
        _scene = scene;
        _collision = collision;

        _levelPalette[WallLevel] = WallPalette;
        _levelPalette[RemoteHumanLevel] = RemoteHumanPalette;
        _levelPalette[120] = MonsterPalette120;
        _levelPalette[130] = MonsterPalette130;
        _levelPalette[140] = MonsterPalette140;
        _levelPalette[150] = MonsterPalette150;
        _levelPalette[LocalPlayerLevel] = LocalPlayerPalette;

        Visible = false;
        TargetBlend = BlendMode.Transparent;
    }

    public void SetPalette(ReadOnlySpan<ushort> colors)
    {
        for (var index = 0; index < _colors.Length; index++)
        {
            _colors[index] = index < colors.Length ? colors[index] : (ushort)0;
        }

        RequestFullRefresh();
    }

    public int Scale => _scale;

    public bool ZoomEnabled { get; set; }

    public int CenterX => _centerX;

    public int MapWidth => _mapWidth;

    public int MapHeight => _mapHeight;

    public bool RestrictedVisibility { get; private set; }

    public int CellsDrawn { get; private set; }

    public int ProjectionOffsetY => Bounds.Height > 400 ? -0x1C : 0;

    public void SetScale(int scale)
    {
        _scale = scale;
        RequestFullRefresh();
    }

    public void SetCenter(int x, int y)
    {
        _centerX = x;
        _centerY = y;
        RequestFullRefresh();
    }

    public MapFile? Cells => _cells;

    public void SetMap(MapFile? cells, int width, int height)
    {
        _cells = cells;
        SetMapSize(width, height);
    }

    public void SetMapSize(int width, int height)
    {
        _mapWidth = Math.Clamp(width, 0, CellStride);
        _mapHeight = Math.Clamp(height, 0, CellStride);

        Array.Clear(_refreshRequested);
        _requestedCount = 0;
        RebuildStaticLevels();

        RequestFullRefresh();
        ClearVisibilityRegions();
        SetRestrictedVisibility(false);
    }

    public void RequestFullRefresh()
    {
        _forceRefresh = true;

        if (Visible)
        {
            Invalidate();
        }
    }

    public void MarkCellDirty(int x, int y)
    {
        if (RequestCell(x, y) && Visible)
        {
            Invalidate();
        }
    }

    private bool RequestCell(int x, int y)
    {
        if ((uint)x >= CellStride || (uint)y >= CellStride)
        {
            return false;
        }

        var index = (y * CellStride) + x;
        if (_refreshRequested[index] != 0)
        {
            return false;
        }

        _refreshRequested[index] = 1;
        _requested[_requestedCount++] = index;
        return true;
    }

    public void SetRestrictedVisibility(bool enabled)
    {
        if (RestrictedVisibility == enabled)
        {
            return;
        }

        RestrictedVisibility = enabled;
        _maskStale = true;
        RequestFullRefresh();
    }

    public void SetVisibilityRegion(EntityId key, int x, int y, int extent)
    {
        for (var slot = 0; slot < _regions.Length; slot++)
        {
            if (_regions[slot].Key == key || _regions[slot].Key == default)
            {
                _regions[slot] = new VisibilityRegion(key, x, y, extent);
                _maskStale = true;
                RequestFullRefresh();
                return;
            }
        }
    }

    public void RemoveVisibilityRegion(EntityId key)
    {
        for (var slot = 0; slot < _regions.Length; slot++)
        {
            if (_regions[slot].Key == key)
            {
                _regions[slot] = default;
                _maskStale = true;
                RequestFullRefresh();
                return;
            }
        }
    }

    public void ClearVisibilityRegions()
    {
        Array.Clear(_regions);
        _maskStale = true;
        RequestFullRefresh();
    }

    public void ApplyClassZoom(int characterClass)
    {
        if (characterClass == ZoomingClass)
        {
            SetScale(ZoomingClassScale);
            ZoomEnabled = true;
        }
        else
        {
            SetScale(DefaultScale);
            ZoomEnabled = false;
        }
    }

    public bool HandleZoomKey(int key)
    {
        switch (key)
        {
            case ZoomInKey:
                if (ZoomEnabled)
                {
                    SetScale(_scale + 1);
                }

                return true;

            case ZoomOutKey:
                if (ZoomEnabled && _scale > MinimumScale)
                {
                    SetScale(_scale - 1);
                }

                return true;

            default:
                return false;
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        RebuildVisibilityMask();
        RefreshCollisionCache();

        if (_forcedThisPass)
        {
            canvas.Clear();
        }

        var scale = _scale;

        var midX = Bounds.Width / 2;
        var midY = (Bounds.Height / 2) + ProjectionOffsetY;

        CellsDrawn = 0;

        var sumLow = FloorDiv(-scale - midY, scale);
        var sumHigh = CeilDiv(Bounds.Height + scale - midY, scale);
        var differenceLow = FloorDiv((-2 * scale) - midX, 2 * scale);
        var differenceHigh = CeilDiv(Bounds.Width + (2 * scale) - midX, 2 * scale);

        var minX = Math.Max(0, FloorDiv(sumLow + differenceLow, 2) + _centerX);
        var maxX = Math.Min(_mapWidth - 1, CeilDiv(sumHigh + differenceHigh, 2) + _centerX);
        var minY = Math.Max(0, FloorDiv(sumLow - differenceHigh, 2) + _centerY);
        var maxY = Math.Min(_mapHeight - 1, CeilDiv(sumHigh - differenceLow, 2) + _centerY);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var pixelX = (2 * scale * ((x - _centerX) - (y - _centerY))) + midX;
                var pixelY = (scale * ((x - _centerX) + (y - _centerY))) + midY;

                if (pixelX + (2 * scale) < 0
                    || pixelX - (2 * scale) > Bounds.Width
                    || pixelY + scale < 0
                    || pixelY - scale > Bounds.Height)
                {
                    continue;
                }

                CellsDrawn++;
                var index = (y * CellStride) + x;
                var level = _level[index];

                if (RestrictedVisibility && _cellVisible[index] == 0 && level != LocalPlayerLevel)
                {
                    FillDiamond(canvas, pixelY, pixelX, _colors[0], scale - 1, dither: false);
                }
                else
                {
                    if (_redrawPending[index] != 0)
                    {
                        PaintCell(canvas, pixelY, pixelX, level, scale);
                        _redrawPending[index] = 0;
                    }
                }

                DrawWallEdges(canvas, x, y, index, pixelY, pixelX, scale);
            }
        }
    }

    private static int FloorDiv(int value, int divisor) =>
        (int)Math.Floor((double)value / divisor);

    private static int CeilDiv(int value, int divisor) =>
        (int)Math.Ceiling((double)value / divisor);

    private void PaintCell(Canvas canvas, int pixelY, int pixelX, byte level, int scale)
    {
        if (level == WallLevel)
        {
            FillDiamond(canvas, pixelY, pixelX, _colors[_levelPalette[level]], scale, dither: true);
        }
        else if (level != 0)
        {
            FillDiamond(canvas, pixelY, pixelX, _colors[_levelPalette[level]], scale - 1, dither: false);
        }
        else if (_forcedThisPass)
        {
            FillDiamond(canvas, pixelY, pixelX, _colors[0], scale - 1, dither: false);
        }
    }

    private void DrawWallEdges(Canvas canvas, int x, int y, int index, int pixelY, int pixelX, int scale)
    {
        var wall = _level[index] == WallLevel;

        if (x > 0
            && wall != (_level[index - 1] == WallLevel)
            && (!RestrictedVisibility || _cellVisible[index - 1] != 0))
        {
            DrawNorthwestEdge(canvas, pixelY, pixelX, scale);
        }

        if (y > 0
            && wall != (_level[index - CellStride] == WallLevel)
            && (!RestrictedVisibility || _cellVisible[index - CellStride] != 0))
        {
            DrawNortheastEdge(canvas, pixelY, pixelX, scale);
        }
    }

    private static void FillDiamond(Canvas canvas, int centerY, int centerX, ushort color, int radius, bool dither)
    {
        var r = (byte)radius;
        canvas.State.Dither = dither ? DitherMode.Halftone : DitherMode.None;

        var left = centerX;
        var right = centerX;

        for (var y = centerY - r; y <= centerY; y++)
        {
            canvas.DrawHorizontalSpan(left, y, right - left + 1, color);
            left -= 2;
            right += 2;
        }

        left += 3;
        right -= 3;

        for (var y = centerY + 1; y <= centerY + r; y++)
        {
            canvas.DrawHorizontalSpan(left, y, right - left + 1, color);
            left += 2;
            right -= 2;
        }

        canvas.State.Dither = DitherMode.None;
    }

    private void DrawNorthwestEdge(Canvas canvas, int centerY, int centerX, int scale)
    {
        if (!canvas.TryBeginPixels(out var writer))
        {
            return;
        }

        var color = _colors[WallEdgePalette];
        var x = centerX;
        var y = centerY - scale;

        for (var k = 0; k <= 2 * scale; k++)
        {
            writer.Set(x, y, color);
            x--;

            if ((k & 1) != 0)
            {
                y++;
            }
        }
    }

    private void DrawNortheastEdge(Canvas canvas, int centerY, int centerX, int scale)
    {
        if (!canvas.TryBeginPixels(out var writer))
        {
            return;
        }

        var color = _colors[WallEdgePalette];
        var x = centerX;
        var y = centerY - scale;

        for (var k = 0; k <= 2 * scale; k++)
        {
            writer.Set(x, y, color);
            x++;

            if ((k & 1) != 0)
            {
                y++;
            }
        }
    }

    private void RefreshCollisionCache()
    {
        var reindexes = _scene.Objects.ReindexCount;
        if (reindexes != _reindexes)
        {
            _reindexes = reindexes;
            _forceRefresh = true;
        }

        RebuildObjectLevels();

        if (_forceRefresh)
        {
            for (var y = 0; y < _mapHeight; y++)
            {
                var row = y * CellStride;
                for (var x = 0; x < _mapWidth; x++)
                {
                    var index = row + x;
                    _level[index] = Math.Max(_staticLevel[index], _objectLevel[index]);
                    _redrawPending[index] = 1;
                }
            }
        }
        else
        {
            for (var slot = 0; slot < _requestedCount; slot++)
            {
                var index = _requested[slot];
                _level[index] = Math.Max(_staticLevel[index], _objectLevel[index]);
                _redrawPending[index] = 1;
            }
        }

        _forcedThisPass = _forceRefresh;
        _forceRefresh = false;
    }

    private void RebuildObjectLevels()
    {
        for (var slot = 0; slot < _touchedCount; slot++)
        {
            _objectLevel[_touched[slot]] = 0;
        }

        _touchedCount = 0;

        var objects = _scene.Objects;
        for (var slot = 0; slot < WorldObjectTable.Capacity; slot++)
        {
            if (!objects.InWorldObjectList(slot))
            {
                continue;
            }

            ref readonly var entry = ref objects.EntryAt(slot);

            var at = objects.CommittedPositionAt(slot);
            if ((uint)at.X >= CellStride || (uint)at.Y >= CellStride)
            {
                continue;
            }

            var level = LevelOf(in entry);
            if (level == 0)
            {
                continue;
            }

            var index = (at.Y * CellStride) + at.X;
            if (_objectLevel[index] == 0)
            {
                _touched[_touchedCount++] = index;
            }

            if (level > _objectLevel[index])
            {
                _objectLevel[index] = level;
            }

            RequestCell(at.X, at.Y);
        }
    }

    private void RebuildStaticLevels()
    {
        Array.Clear(_staticLevel);

        if (_cells is not { } map)
        {
            return;
        }

        for (var y = 0; y < _mapHeight; y++)
        {
            var row = y * CellStride;
            for (var x = 0; x < _mapWidth; x++)
            {
                var cell = map.GetCell(x, y);
                var left = cell.LeftStaticTile == EmptyTileId ? (ushort)0 : cell.LeftStaticTile;
                var right = cell.RightStaticTile == EmptyTileId ? (ushort)0 : cell.RightStaticTile;

                var flags = _collision.GetCollisionFlags(left) | _collision.GetCollisionFlags(right);
                _staticLevel[row + x] = flags == SotpTable.CollisionMask ? WallLevel : (byte)0;
            }
        }
    }

    private static byte LevelOf(in WorldObjectEntry entry)
    {
        if (entry.IsLocalPlayer)
        {
            return LocalPlayerLevel;
        }

        return entry.Kind switch
        {
            WorldObjectKind.Human or WorldObjectKind.DisguisedHuman =>
                entry.Appearance.Appearance.Body.BodySprite == 0 ? (byte)0 : RemoteHumanLevel,
            WorldObjectKind.Creature or WorldObjectKind.Mundane => entry.CreatureType switch
            {
                CreatureType.Passable => 150,
                CreatureType.Mundane => 140,
                CreatureType.Solid => 130,
                _ => 120,
            },
            _ => 0,
        };
    }

    private readonly VisibilityRegion[] _regions = new VisibilityRegion[WorldObjectTable.Capacity];

    private bool _maskStale = true;

    private void RebuildVisibilityMask()
    {
        if (!RestrictedVisibility || !_maskStale)
        {
            return;
        }

        _maskStale = false;
        Array.Clear(_cellVisible);

        for (var slot = 0; slot < _regions.Length; slot++)
        {
            var region = _regions[slot];
            if (region.Key == default)
            {
                continue;
            }

            var left = Math.Max(region.X - region.Extent, 0);
            var top = Math.Max(region.Y - region.Extent, 0);
            var right = Math.Min(region.X + region.Extent + 1, CellStride);
            var bottom = Math.Min(region.Y + region.Extent + 1, CellStride);

            for (var y = top; y < bottom; y++)
            {
                var row = y * CellStride;
                for (var x = left; x < right; x++)
                {
                    _cellVisible[row + x] = 1;
                }
            }
        }
    }

    private readonly record struct VisibilityRegion(EntityId Key, int X, int Y, int Extent);
}
