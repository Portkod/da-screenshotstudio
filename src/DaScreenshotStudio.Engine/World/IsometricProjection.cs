using DaScreenshotStudio.Assets.Imaging;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public static class IsometricProjection
{
    public const int TileWidth = GroundTileBank.TileWidth;

    public const int TileHeight = GroundTileBank.TileHeight;

    public const int HalfTileWidth = TileWidth / 2;

    public const int HalfTileHeight = (TileHeight + 1) / 2;

    public const int StaticWidth = HalfTileWidth;

    public static void Project(int cellX, int cellY, out int worldX, out int worldY)
    {
        worldX = (cellX - cellY) * HalfTileWidth;
        worldY = (cellX + cellY) * HalfTileHeight;
    }

    public static void Project(in MapPoint cell, out int worldX, out int worldY) =>
        Project(cell.X, cell.Y, out worldX, out worldY);

    public static void CellAnchor(int cellX, int cellY, out int worldX, out int worldY)
    {
        Project(cellX, cellY, out worldX, out worldY);
        worldX += HalfTileWidth;
        worldY += HalfTileHeight;
    }

    public static void CellAnchor(in MapPoint cell, out int worldX, out int worldY) =>
        CellAnchor(cell.X, cell.Y, out worldX, out worldY);

    public static void Unproject(int worldX, int worldY, out int cellX, out int cellY)
    {
        var across = FloorDivide(worldX, HalfTileWidth);
        var down = FloorDivide(worldY, HalfTileHeight);

        cellX = FloorDivide(across + down, 2);
        cellY = FloorDivide(down - across, 2);
    }

    public static void CellAt(int worldX, int worldY, out int cellX, out int cellY)
    {
        var dx = worldX - HalfTileWidth;
        var dy = worldY - HalfTileHeight;

        cellX = RoundToCell((HalfTileHeight * dx) + (HalfTileWidth * dy));
        cellY = RoundToCell((HalfTileWidth * dy) - (HalfTileHeight * dx));
    }

    private const int CellUnit = TileWidth * HalfTileHeight;

    private static int RoundToCell(int rotated) => FloorDivide(rotated + (CellUnit / 2), CellUnit);

    public static void VisibleCellRange(
        int worldLeft,
        int worldTop,
        int worldRight,
        int worldBottom,
        out int minCellX,
        out int minCellY,
        out int maxCellX,
        out int maxCellY)
    {
        worldLeft -= TileWidth;
        worldTop -= TileHeight;

        Unproject(worldLeft, worldTop, out var ax, out var ay);
        Unproject(worldRight, worldTop, out var bx, out var by);
        Unproject(worldLeft, worldBottom, out var cx, out var cy);
        Unproject(worldRight, worldBottom, out var dx, out var dy);

        minCellX = Math.Min(Math.Min(ax, bx), Math.Min(cx, dx));
        maxCellX = Math.Max(Math.Max(ax, bx), Math.Max(cx, dx));
        minCellY = Math.Min(Math.Min(ay, by), Math.Min(cy, dy));
        maxCellY = Math.Max(Math.Max(ay, by), Math.Max(cy, dy));
    }

    public const int StaticBottom = HalfTileHeight * 2;

    public static int StaticTop(int cellWorldY, int artHeight) => cellWorldY + StaticBottom - artHeight;

    private static int FloorDivide(int value, int divisor) =>
        value >= 0 ? value / divisor : -(((-value) + divisor - 1) / divisor);
}
