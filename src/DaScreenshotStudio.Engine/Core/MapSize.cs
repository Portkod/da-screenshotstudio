using System.Globalization;

namespace DaScreenshotStudio.Engine.Core;

public readonly struct MapSize : IEquatable<MapSize>
{
    public MapSize(byte width, byte height)
    {
        Width = width;
        Height = height;
    }

    public byte Width { get; }

    public byte Height { get; }

    public bool Equals(MapSize other) => Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is MapSize other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(MapSize left, MapSize right) => left.Equals(right);

    public static bool operator !=(MapSize left, MapSize right) => !left.Equals(right);

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Width}x{Height}");
}
