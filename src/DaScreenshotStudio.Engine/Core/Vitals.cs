namespace DaScreenshotStudio.Engine.Core;

public readonly struct Vitals : IEquatable<Vitals>
{
    public Vitals(uint health, uint maxHealth, uint mana, uint maxMana)
    {
        Health = health;
        MaxHealth = maxHealth;
        Mana = mana;
        MaxMana = maxMana;
    }

    public uint Health { get; }

    public uint MaxHealth { get; }

    public uint Mana { get; }

    public uint MaxMana { get; }

    public bool Equals(Vitals other) =>
        Health == other.Health &&
        MaxHealth == other.MaxHealth &&
        Mana == other.Mana &&
        MaxMana == other.MaxMana;

    public override bool Equals(object? obj) => obj is Vitals other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Health, MaxHealth, Mana, MaxMana);

    public static bool operator ==(Vitals left, Vitals right) => left.Equals(right);

    public static bool operator !=(Vitals left, Vitals right) => !left.Equals(right);
}
