using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class MapBtmLayer
{
    private Record[] _records = [];
    private int[] _hidden = [];
    private int[] _visible = [];
    private int _hiddenCount;
    private int _visibleCount;

    public MapBottomImages? Images { get; private set; }

    public int MapWidth { get; private set; }

    public int MapHeight { get; private set; }

    public int DeclaredCount { get; private set; }

    public bool IsActive { get; private set; }

    public void SetDimensions(int mapWidth, int mapHeight)
    {
        MapWidth = mapWidth;
        MapHeight = mapHeight;
    }

    public void Clear()
    {
        _hiddenCount = 0;
        _visibleCount = 0;
        DeclaredCount = 0;
        IsActive = false;
        _records = [];
        Images = null;
    }

    public bool TryLoadFrom(MetafileStore metafiles, IAssetArchive archive, int mapId, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(metafiles);
        ArgumentNullException.ThrowIfNull(archive);

        Clear();

        if (metafiles.Find(MapBottomLayer.MetadataNameFor(mapId)) is not { } cached)
        {
            return false;
        }

        MapBottomPlacementTable table;
        try
        {
            table = MapBottomPlacementTable.Parse(cached);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        Load(table, new MapBottomImages(archive, in packing));
        return true;
    }

    public void Load(MapBottomPlacementTable table, MapBottomImages images)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(images);

        Clear();

        var placements = table.Placements;
        var records = new Record[placements.Length];
        var count = 0;

        for (var i = 0; i < placements.Length; i++)
        {
            var placement = placements[i];
            var index = images.Resolve((ushort)placement.ImageId);
            if (index < 0)
            {
                continue;
            }

            var image = images[index];
            records[count++] = new Record(
                new PixelRect(placement.Left, placement.Top, image.Width, image.Height),
                index);
        }

        _records = count == records.Length ? records : records[..count];
        _hidden = new int[count];
        _visible = new int[count];
        _hiddenCount = count;

        for (var i = 0; i < count; i++)
        {
            _hidden[i] = i;
        }

        DeclaredCount = table.Count;
        Images = images;
        IsActive = true;
    }

    public void UpdateVisiblePlacements(in PixelRect view)
    {
        for (var i = 0; i < _hiddenCount;)
        {
            var slot = _hidden[i];
            if (!Overlaps(in view, in _records[slot].Bounds))
            {
                i++;
                continue;
            }

            _visible[_visibleCount++] = slot;
            RemoveAt(_hidden, ref _hiddenCount, i);
        }

        for (var i = 0; i < _visibleCount;)
        {
            var slot = _visible[i];
            if (Overlaps(in view, in _records[slot].Bounds))
            {
                i++;
                continue;
            }

            _hidden[_hiddenCount++] = slot;
            RemoveAt(_visible, ref _visibleCount, i);
        }
    }

    public int Draw(Canvas canvas, int originX, int originY)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (Images is not { } images)
        {
            return 0;
        }

        var view = canvas.Clip.Translate(originX, originY);
        var drawn = 0;

        for (var i = 0; i < _visibleCount; i++)
        {
            ref readonly var record = ref _records[_visible[i]];
            if (!view.IntersectsWith(record.Bounds))
            {
                continue;
            }

            canvas.BlitPixmap(
                record.Bounds.X - originX,
                record.Bounds.Y - originY,
                images[record.ImageIndex],
                BlendMode.Transparent);

            drawn++;
        }

        return drawn;
    }

    private static bool Overlaps(in PixelRect view, in PixelRect bounds) =>
        view.X <= bounds.Right
        && view.Right >= bounds.X
        && view.Y <= bounds.Bottom
        && view.Bottom >= bounds.Y;

    private static void RemoveAt(int[] list, ref int count, int index)
    {
        var span = list.AsSpan(0, count);
        span[(index + 1)..].CopyTo(span[index..]);
        count--;
    }

    private readonly struct Record(in PixelRect bounds, int imageIndex)
    {
        public readonly PixelRect Bounds = bounds;

        public readonly int ImageIndex = imageIndex;
    }
}
