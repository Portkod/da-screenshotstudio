using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Records;

public readonly struct StatusModifiers : IEquatable<StatusModifiers>
{
    public const byte BlindedCode = 0x08;

    public byte BlindCode { get; init; }

    public byte MailState { get; init; }

    public Element AttackElement { get; init; }

    public Element DefenseElement { get; init; }

    public byte MagicResistUnits { get; init; }

    public sbyte ArmorClass { get; init; }

    public byte DamageModifier { get; init; }

    public byte HitModifier { get; init; }

    public bool IsBlinded => BlindCode == BlindedCode;

    public bool HasHighMailNibble => (MailState & 0xF0) != 0;

    public bool Equals(StatusModifiers other) =>
        BlindCode == other.BlindCode &&
        MailState == other.MailState &&
        AttackElement == other.AttackElement &&
        DefenseElement == other.DefenseElement &&
        MagicResistUnits == other.MagicResistUnits &&
        ArmorClass == other.ArmorClass &&
        DamageModifier == other.DamageModifier &&
        HitModifier == other.HitModifier;

    public override bool Equals(object? obj) => obj is StatusModifiers other && Equals(other);

    public override int GetHashCode()
    {
        var first = HashCode.Combine(BlindCode, MailState, AttackElement, DefenseElement);
        return HashCode.Combine(first, MagicResistUnits, ArmorClass, DamageModifier, HitModifier);
    }

    public static bool operator ==(StatusModifiers left, StatusModifiers right) => left.Equals(right);

    public static bool operator !=(StatusModifiers left, StatusModifiers right) => !left.Equals(right);
}
