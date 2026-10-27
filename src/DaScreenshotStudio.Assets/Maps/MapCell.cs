namespace DaScreenshotStudio.Assets.Maps;

public readonly struct MapCell : IEquatable<MapCell>
{
    public const int Length = 6;

    public const int MaxTileId = ushort.MaxValue;

    public MapCell(ushort groundTile, ushort leftStaticTile, ushort rightStaticTile)
    {
        GroundTile = groundTile;
        LeftStaticTile = leftStaticTile;
        RightStaticTile = rightStaticTile;
    }

    public ushort GroundTile { get; }

    public ushort LeftStaticTile { get; }

    public ushort RightStaticTile { get; }

    public bool Equals(MapCell other) =>
        GroundTile == other.GroundTile
        && LeftStaticTile == other.LeftStaticTile
        && RightStaticTile == other.RightStaticTile;

    public override bool Equals(object? obj) => obj is MapCell other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(GroundTile, LeftStaticTile, RightStaticTile);

    public override string ToString() => $"{GroundTile}/{LeftStaticTile}/{RightStaticTile}";

    public static bool operator ==(MapCell left, MapCell right) => left.Equals(right);

    public static bool operator !=(MapCell left, MapCell right) => !left.Equals(right);
}
