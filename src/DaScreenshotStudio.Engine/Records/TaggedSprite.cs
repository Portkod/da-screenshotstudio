namespace DaScreenshotStudio.Engine.Records;

public readonly struct TaggedSprite : IEquatable<TaggedSprite>
{
    public const ushort CreatureTag = 0x4000;

    public const ushort ItemTag = 0x8000;

    public TaggedSprite(ushort value) => Value = value;

    public ushort Value { get; }

    public SpriteNamespace Namespace => Value switch
    {
        >= CreatureTag and < 0x8000 => SpriteNamespace.Creature,
        >= ItemTag and < 0xC000 => SpriteNamespace.Item,
        _ => SpriteNamespace.None,
    };

    public ushort Sprite => Namespace switch
    {
        SpriteNamespace.Creature => (ushort)(Value - CreatureTag),
        SpriteNamespace.Item => (ushort)(Value - ItemTag),
        _ => 0,
    };

    public static TaggedSprite Creature(ushort sprite) => new((ushort)(sprite + CreatureTag));

    public static TaggedSprite Item(ushort sprite) => new((ushort)(sprite + ItemTag));

    public bool Equals(TaggedSprite other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is TaggedSprite other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(TaggedSprite left, TaggedSprite right) => left.Equals(right);

    public static bool operator !=(TaggedSprite left, TaggedSprite right) => !left.Equals(right);
}
