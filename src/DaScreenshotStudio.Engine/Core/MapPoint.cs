using System.Globalization;

namespace DaScreenshotStudio.Engine.Core;

public readonly struct MapPoint : IEquatable<MapPoint>
{
    public MapPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public bool Equals(MapPoint other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is MapPoint other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(MapPoint left, MapPoint right) => left.Equals(right);

    public static bool operator !=(MapPoint left, MapPoint right) => !left.Equals(right);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X}, {Y})");
}
