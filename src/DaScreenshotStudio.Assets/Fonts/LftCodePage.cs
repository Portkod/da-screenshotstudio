namespace DaScreenshotStudio.Assets.Fonts;

public readonly struct LftCodePage : IEquatable<LftCodePage>
{
    private readonly byte _firstLow;
    private readonly byte _lastLow;
    private readonly byte _firstHigh;
    private readonly byte _lastHigh;

    public LftCodePage(int identifier, byte firstLow, byte lastLow, byte firstHigh = 1, byte lastHigh = 0)
    {
        Identifier = identifier;
        _firstLow = firstLow;
        _lastLow = lastLow;
        _firstHigh = firstHigh;
        _lastHigh = lastHigh;
    }

    public int Identifier { get; }

    public static LftCodePage SingleByte { get; } = new(0, 1, 0);

    public static LftCodePage Korean949 { get; } = new(949, 0x81, 0xFE);

    public static LftCodePage Japanese932 { get; } = new(932, 0x81, 0x9F, 0xE0, 0xFC);

    public bool IsLeadByte(byte value) =>
        (value >= _firstLow && value <= _lastLow) || (value >= _firstHigh && value <= _lastHigh);

    public bool Equals(LftCodePage other) =>
        Identifier == other.Identifier && _firstLow == other._firstLow && _lastLow == other._lastLow
        && _firstHigh == other._firstHigh && _lastHigh == other._lastHigh;

    public override bool Equals(object? obj) => obj is LftCodePage other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Identifier, _firstLow, _lastLow, _firstHigh, _lastHigh);

    public static bool operator ==(LftCodePage left, LftCodePage right) => left.Equals(right);

    public static bool operator !=(LftCodePage left, LftCodePage right) => !left.Equals(right);
}

public ref struct LftKeyReader
{
    private readonly LftCodePage _codePage;
    private ReadOnlySpan<byte> _remaining;

    public LftKeyReader(ReadOnlySpan<byte> text, LftCodePage codePage)
    {
        _remaining = text;
        _codePage = codePage;
    }

    public bool TryReadKey(out int key)
    {
        if (_remaining.IsEmpty)
        {
            key = 0;
            return false;
        }

        var first = _remaining[0];
        if (_codePage.IsLeadByte(first))
        {
            if (_remaining.Length < 2)
            {
                _remaining = default;
                key = 0;
                return false;
            }

            key = (first << 8) | _remaining[1];
            _remaining = _remaining[2..];
            return true;
        }

        key = first;
        _remaining = _remaining[1..];
        return true;
    }
}
