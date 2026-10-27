namespace DaScreenshotStudio.Assets.Gui;

public enum GuiLayoutTokenKind
{
    Tag,

    Text,

    Number,
}

public readonly ref struct GuiLayoutToken
{
    internal GuiLayoutToken(GuiLayoutTokenKind kind, ReadOnlySpan<byte> text, int number)
    {
        Kind = kind;
        Text = text;
        Number = number;
    }

    public GuiLayoutTokenKind Kind { get; }

    public ReadOnlySpan<byte> Text { get; }

    public int Number { get; }
}

public ref struct GuiLayoutTokenizer
{
    private ReadOnlySpan<byte> _remaining;

    public GuiLayoutTokenizer(ReadOnlySpan<byte> data) => _remaining = data;

    public bool TryReadToken(out GuiLayoutToken token)
    {
        while (!_remaining.IsEmpty && _remaining[0] <= (byte)' ')
        {
            _remaining = _remaining[1..];
        }

        if (_remaining.IsEmpty)
        {
            token = default;
            return false;
        }

        var first = _remaining[0];

        if (first == (byte)'<')
        {
            _remaining = _remaining[1..];
            var end = _remaining.IndexOf((byte)'>');
            if (end < 0)
            {
                end = _remaining.Length;
                token = new GuiLayoutToken(GuiLayoutTokenKind.Tag, _remaining, 0);
                _remaining = default;
                return true;
            }

            token = new GuiLayoutToken(GuiLayoutTokenKind.Tag, _remaining[..end], 0);
            _remaining = _remaining[(end + 1)..];
            return true;
        }

        if (first == (byte)'"')
        {
            _remaining = _remaining[1..];
            var end = _remaining.IndexOf((byte)'"');
            if (end < 0)
            {
                token = new GuiLayoutToken(GuiLayoutTokenKind.Text, _remaining, 0);
                _remaining = default;
                return true;
            }

            token = new GuiLayoutToken(GuiLayoutTokenKind.Text, _remaining[..end], 0);
            _remaining = _remaining[(end + 1)..];
            return true;
        }

        var length = 0;
        while (length < _remaining.Length && _remaining[length] > (byte)' ' && _remaining[length] != (byte)'<' && _remaining[length] != (byte)'"')
        {
            length++;
        }

        var raw = _remaining[..length];
        _remaining = _remaining[length..];

        token = TryParseNumber(raw, out var value)
            ? new GuiLayoutToken(GuiLayoutTokenKind.Number, raw, value)
            : new GuiLayoutToken(GuiLayoutTokenKind.Text, raw, 0);

        return true;
    }

    private static bool TryParseNumber(ReadOnlySpan<byte> raw, out int value)
    {
        value = 0;
        if (raw.IsEmpty)
        {
            return false;
        }

        var index = 0;
        var negative = raw[0] == (byte)'-';
        if (negative)
        {
            index = 1;
        }

        if (index >= raw.Length)
        {
            return false;
        }

        for (; index < raw.Length; index++)
        {
            var digit = (uint)(raw[index] - (byte)'0');
            if (digit > 9)
            {
                return false;
            }

            value = (value * 10) + (int)digit;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }
}
