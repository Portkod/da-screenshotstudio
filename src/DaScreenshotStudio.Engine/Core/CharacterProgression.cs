namespace DaScreenshotStudio.Engine.Core;

public readonly struct CharacterProgression : IEquatable<CharacterProgression>
{
    public CharacterProgression(
        byte level,
        byte abilityLevel,
        uint totalExperience,
        uint toNextLevel,
        uint totalAbility,
        uint toNextAbility,
        uint gamePoints,
        uint gold)
    {
        Level = level;
        AbilityLevel = abilityLevel;
        TotalExperience = totalExperience;
        ToNextLevel = toNextLevel;
        TotalAbility = totalAbility;
        ToNextAbility = toNextAbility;
        GamePoints = gamePoints;
        Gold = gold;
    }

    public byte Level { get; }

    public byte AbilityLevel { get; }

    public uint TotalExperience { get; }

    public uint ToNextLevel { get; }

    public uint TotalAbility { get; }

    public uint ToNextAbility { get; }

    public uint GamePoints { get; }

    public uint Gold { get; }

    public bool Equals(CharacterProgression other) =>
        Level == other.Level &&
        AbilityLevel == other.AbilityLevel &&
        TotalExperience == other.TotalExperience &&
        ToNextLevel == other.ToNextLevel &&
        TotalAbility == other.TotalAbility &&
        ToNextAbility == other.ToNextAbility &&
        GamePoints == other.GamePoints &&
        Gold == other.Gold;

    public override bool Equals(object? obj) => obj is CharacterProgression other && Equals(other);

    public override int GetHashCode()
    {
        var first = HashCode.Combine(Level, AbilityLevel, TotalExperience, ToNextLevel);
        return HashCode.Combine(first, TotalAbility, ToNextAbility, GamePoints, Gold);
    }

    public static bool operator ==(CharacterProgression left, CharacterProgression right) => left.Equals(right);

    public static bool operator !=(CharacterProgression left, CharacterProgression right) => !left.Equals(right);
}
