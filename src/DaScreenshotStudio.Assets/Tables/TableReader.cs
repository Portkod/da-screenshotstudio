namespace DaScreenshotStudio.Assets.Tables;

public ref struct TableReader
{
    private ReadOnlySpan<byte> _remaining;

    public TableReader(ReadOnlySpan<byte> data) => _remaining = data;

    public bool TryReadLine(out TableLine line)
    {
        while (!_remaining.IsEmpty)
        {
            var raw = Trim(TakeLine(ref _remaining, "//"u8));
            if (!raw.IsEmpty)
            {
                line = new TableLine(raw);
                return true;
            }
        }

        line = default;
        return false;
    }

    internal static ReadOnlySpan<byte> TakeLine(
        scoped ref ReadOnlySpan<byte> remaining,
        scoped ReadOnlySpan<byte> comment)
    {
        var end = remaining.IndexOfAny((byte)'\r', (byte)'\n');
        ReadOnlySpan<byte> raw;
        if (end < 0)
        {
            raw = remaining;
            remaining = default;
        }
        else
        {
            raw = remaining[..end];
            remaining = remaining[(end + 1)..];
        }

        var at = raw.IndexOf(comment);
        return at >= 0 ? raw[..at] : raw;
    }

    internal static bool TryReadDigits(scoped ref ReadOnlySpan<byte> remaining, out int value)
    {
        value = 0;

        var digits = 0;
        while (!remaining.IsEmpty && (uint)(remaining[0] - (byte)'0') <= 9)
        {
            value = (value * 10) + (remaining[0] - (byte)'0');
            remaining = remaining[1..];
            digits++;
        }

        return digits > 0;
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
    {
        var start = 0;
        while (start < value.Length && IsSpace(value[start]))
        {
            start++;
        }

        var end = value.Length;
        while (end > start && IsSpace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    internal static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v';
}

public ref struct TableLine
{
    private ReadOnlySpan<byte> _remaining;

    internal TableLine(ReadOnlySpan<byte> text)
    {
        Text = text;
        _remaining = text;
    }

    public ReadOnlySpan<byte> Text { get; }

    public bool TryReadInt(out int value)
    {
        while (!_remaining.IsEmpty && TableReader.IsSpace(_remaining[0]))
        {
            _remaining = _remaining[1..];
        }

        value = 0;
        if (_remaining.IsEmpty)
        {
            return false;
        }

        var negative = _remaining[0] == (byte)'-';
        if (negative)
        {
            _remaining = _remaining[1..];
        }

        if (!TableReader.TryReadDigits(ref _remaining, out value))
        {
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    public int ReadInts(scoped Span<int> values)
    {
        var count = 0;
        while (count < values.Length && TryReadInt(out var value))
        {
            values[count++] = value;
        }

        return count;
    }
}
