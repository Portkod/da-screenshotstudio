using DaScreenshotStudio.Assets.Archives;
using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public sealed class MapArtCaches
{
    public MapArtCaches(IAssetArchive archive, MapArt art, bool alternate, in PixelPacking packing)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(art);

        Alternate = alternate;
        Palettes = new PackedPaletteCache(in packing);
        Ground = new GroundTileCache(art, Palettes, alternate, art.GroundBank.Count + 1);
        Statics = new StaticTileCache(archive, art, Palettes, alternate, art.Collision.HighestTileId + 1);
    }

    public bool Alternate { get; }

    public PackedPaletteCache Palettes { get; }

    public GroundTileCache Ground { get; }

    public StaticTileCache Statics { get; }
}
