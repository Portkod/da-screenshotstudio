namespace DaScreenshotStudio.Assets.Archives;

public readonly struct DataArchiveEntry : IEquatable<DataArchiveEntry>
{
    public const int RowLength = 17;

    public const int NameLength = 13;

    internal DataArchiveEntry(string name, int offset, int length)
    {
        Name = name;
        Offset = offset;
        Length = length;
    }

    public string Name { get; }

    public int Offset { get; }

    public int Length { get; }

    public bool Equals(DataArchiveEntry other) =>
        Offset == other.Offset && Length == other.Length && string.Equals(Name, other.Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DataArchiveEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Name, Offset, Length);

    public override string ToString() => Name;

    public static bool operator ==(DataArchiveEntry left, DataArchiveEntry right) => left.Equals(right);

    public static bool operator !=(DataArchiveEntry left, DataArchiveEntry right) => !left.Equals(right);
}
