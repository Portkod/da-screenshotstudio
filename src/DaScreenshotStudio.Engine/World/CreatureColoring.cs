using DaScreenshotStudio.Assets.Imaging;

namespace DaScreenshotStudio.Engine.World;

public readonly struct CreatureColoring : IEquatable<CreatureColoring>
{
    public const int SelectorCount = MpfImage.MaxColoringRanges;

    private readonly uint _selectors;
    private readonly bool _installed;

    private CreatureColoring(uint selectors, bool installed)
    {
        _selectors = selectors;
        _installed = installed;
    }

    public static CreatureColoring None => default;

    public static CreatureColoring From(ReadOnlySpan<byte> selectors)
    {
        uint packed = 0;
        var kept = Math.Min(selectors.Length, SelectorCount);
        for (var i = 0; i < kept; i++)
        {
            packed |= (uint)selectors[i] << (i * 8);
        }

        return new CreatureColoring(packed, installed: true);
    }

    public static CreatureColoring FromPacked(uint selectors) => new(selectors, installed: true);

    public bool IsInstalled => _installed;

    public byte this[int index] =>
        (uint)index < SelectorCount ? (byte)(_selectors >> (index * 8)) : (byte)0;

    public CreatureColoring AdmittedFor(int ranges)
    {
        if (!_installed || ranges <= 0)
        {
            return None;
        }

        if (ranges >= SelectorCount)
        {
            return this;
        }

        return new CreatureColoring(_selectors & ((1u << (ranges * 8)) - 1), installed: true);
    }

    public bool Equals(CreatureColoring other) =>
        _selectors == other._selectors && _installed == other._installed;

    public override bool Equals(object? obj) => obj is CreatureColoring other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_selectors, _installed);

    public static bool operator ==(CreatureColoring left, CreatureColoring right) => left.Equals(right);

    public static bool operator !=(CreatureColoring left, CreatureColoring right) => !left.Equals(right);
}
