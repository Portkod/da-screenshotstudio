using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.World;

public readonly record struct WorldViewLayout(PixelRect MapRectangle, int CenterBiasY)
{
    public const int MinimalCenterBiasY = 10;

    public const int CompactCenterBiasY = MinimalCenterBiasY + 0x1C;

    public PixelRect PaneBounds =>
        MapRectangle.Inflate(IsometricProjection.HalfTileWidth, IsometricProjection.HalfTileHeight);
}
