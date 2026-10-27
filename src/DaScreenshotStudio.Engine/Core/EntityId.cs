using System.Globalization;

namespace DaScreenshotStudio.Engine.Core;

public readonly struct EntityId : IEquatable<EntityId>
{
    public EntityId(uint value) => Value = value;

    public uint Value { get; }

    public bool IsNone => Value == 0;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
