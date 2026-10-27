namespace DaScreenshotStudio.Engine.Core;

public readonly struct CharacterAttributes : IEquatable<CharacterAttributes>
{
    public CharacterAttributes(
        byte strength,
        byte intelligence,
        byte wisdom,
        byte constitution,
        byte dexterity)
    {
        Strength = strength;
        Intelligence = intelligence;
        Wisdom = wisdom;
        Constitution = constitution;
        Dexterity = dexterity;
    }

    public byte Strength { get; }

    public byte Intelligence { get; }

    public byte Wisdom { get; }

    public byte Constitution { get; }

    public byte Dexterity { get; }

    public bool Equals(CharacterAttributes other) =>
        Strength == other.Strength &&
        Intelligence == other.Intelligence &&
        Wisdom == other.Wisdom &&
        Constitution == other.Constitution &&
        Dexterity == other.Dexterity;

    public override bool Equals(object? obj) => obj is CharacterAttributes other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Strength, Intelligence, Wisdom, Constitution, Dexterity);

    public static bool operator ==(CharacterAttributes left, CharacterAttributes right) => left.Equals(right);

    public static bool operator !=(CharacterAttributes left, CharacterAttributes right) => !left.Equals(right);
}
