namespace DaScreenshotStudio.Assets.Maps;

public static class MapBottomLayer
{
    public const int ImageNameLength = 12;

    public const int HalfTileWidth = 28;

    public const int CanvasOriginY = 574;

    public static string MetadataNameFor(int mapId) => $"{mapId:D6}.mbl";

    public static void CanvasOrigin(int mapHeight, out int originX, out int originY)
    {
        originX = mapHeight * HalfTileWidth;
        originY = CanvasOriginY;
    }

    public static bool TryFormatImageName(int imageId, scoped Span<char> destination)
    {
        if (destination.Length < ImageNameLength || (uint)imageId > 99999)
        {
            return false;
        }

        destination[0] = 'b';
        destination[1] = 't';
        destination[2] = 'm';

        for (var i = 7; i >= 3; i--)
        {
            destination[i] = (char)('0' + (imageId % 10));
            imageId /= 10;
        }

        ".pcx".CopyTo(destination[8..]);
        return true;
    }
}
