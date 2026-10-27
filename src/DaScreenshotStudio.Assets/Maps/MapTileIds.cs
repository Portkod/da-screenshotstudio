namespace DaScreenshotStudio.Assets.Maps;

public static class MapTileIds
{
    public const int Empty = 0;

    public const int EmptyStaticTile = 0x2710;

    public static bool IsDrawnStaticTile(int staticTileId) =>
        staticTileId > 12 && (staticTileId < 10000 || staticTileId > 10012);

    public static int GroundRecord(int groundTileId) => groundTileId - 1;

    public static int StaticArtNumber(int staticTileId) => staticTileId;

    public static int GroundPaletteIndex(int groundTileId) => groundTileId;

    public static int StaticPaletteIndex(int staticTileId) => staticTileId;

    public static int SotpIndex(int staticTileId) => staticTileId - 1;
}
