using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public sealed class WorldCamera
{
    public int WorldX { get; set; }

    public int WorldY { get; set; }

    public void Move(int deltaX, int deltaY)
    {
        WorldX += deltaX;
        WorldY += deltaY;
    }

    public void CenterOnCell(int cellX, int cellY)
    {
        IsometricProjection.Project(cellX, cellY, out var worldX, out var worldY);
        WorldX = worldX + IsometricProjection.HalfTileWidth;
        WorldY = worldY + IsometricProjection.HalfTileHeight;
    }

    public void CenterOnCell(in MapPoint cell) => CenterOnCell(cell.X, cell.Y);
}

public sealed class WorldScene
{
    public WorldScene() => MapLighting = new MapLighting(Lighting);

    private WorldMap? _map;

    public WorldMap? Map
    {
        get => _map;
        set
        {
            _map = value;

            if (value is null)
            {
                return;
            }

            Statics.Reset(value.Cells);
            value.ApplyPaletteCycles(AnimationTicks);
        }
    }

    public WorldCamera Camera { get; } = new();

    public LiveStaticTiles Statics { get; } = new();

    public WorldObjectTable Objects { get; } = new();

    public IWorldObjectArt? ObjectArt { get; set; }

    public IGroupAdMarkerArt? GroupAdArt { get; set; }

    public MapBtmLayer BottomLayer { get; } = new();

    public WorldLighting Lighting { get; } = new();

    public MapLighting MapLighting { get; }

    public DamageMeters Meters { get; } = new();

    public DyingObjects Dying { get; } = new();

    public WorldEffects Effects { get; } = new();

    public MovingEffects MovingEffects { get; } = new();

    public long AnimationTicks { get; set; }

    public long StaticAnimationTicks { get; set; }

    public bool AnimatesStaticObjects { get; set; } = true;

    public bool IsReady => Map is not null;
}
