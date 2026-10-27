using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldMap
{
    private readonly PackedPaletteCache _palettes;

    private WorldMap(
        MapFile cells,
        MapArt art,
        bool alternate,
        PackedPaletteCache palettes,
        GroundTileCache ground,
        StaticTileCache statics)
    {
        Cells = cells;
        Art = art;
        Alternate = alternate;
        _palettes = palettes;
        Ground = ground;
        Statics = statics;
    }

    public MapFile Cells { get; }

    public MapArt Art { get; }

    public bool Alternate { get; }

    public GroundTileCache Ground { get; }

    public StaticTileCache Statics { get; }

    public int Width => Cells.Width;

    public int Height => Cells.Height;

    public static WorldMap Load(IAssetArchive archive, MapArt art, MapFile cells, bool alternate, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(art);

        return Load(cells, art, new MapArtCaches(archive, art, alternate, in packing));
    }

    public static WorldMap Load(MapFile cells, MapArt art, MapArtCaches caches)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(caches);

        caches.Ground.Prewarm(cells);
        caches.Statics.Prewarm(cells);

        return new WorldMap(cells, art, caches.Alternate, caches.Palettes, caches.Ground, caches.Statics);
    }

    public bool ApplyPaletteCycles(long ticks)
    {
        if (!_palettes.ApplyCycles(ticks))
        {
            return false;
        }

        Ground.RepackCycled();
        Statics.RepackCycled();
        return true;
    }

    public MapCell GetCell(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            return default;
        }

        return MapFile.ReadCell(Cells.GetRowBytes(y)[(x * MapCell.Length)..]);
    }
}
