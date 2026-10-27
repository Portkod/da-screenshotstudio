namespace DaScreenshotStudio.Assets.Expressions;

public ref struct ExpressionTokenizer
{
    private ReadOnlySpan<byte> _remaining;

    public ExpressionTokenizer(ReadOnlySpan<byte> source)
    {
        _remaining = source;
    }

    public static bool IsWordByte(byte value) =>
        (value >= (byte)'0' && value <= (byte)'9')
        || (value >= (byte)'A' && value <= (byte)'Z')
        || (value >= (byte)'a' && value <= (byte)'z')
        || value == (byte)'.'
        || value == (byte)'_';

    public static bool IsLeadByte(byte value) => value >= 0x81 && value <= 0xFE;

    public bool TryReadToken(out ReadOnlySpan<byte> token)
    {
        while (!_remaining.IsEmpty)
        {
            var first = _remaining[0];

            if (first <= 0x20)
            {
                _remaining = _remaining[1..];
                continue;
            }

            if (first == (byte)'#')
            {
                var end = _remaining.IndexOfAny((byte)'\r', (byte)'\n');
                _remaining = end < 0 ? default : _remaining[end..];
                continue;
            }

            int length;
            if (IsWordByte(first))
            {
                length = 1;
                while (length < _remaining.Length && IsWordByte(_remaining[length]))
                {
                    length++;
                }
            }
            else if (IsLeadByte(first))
            {
                length = 1;
                while (length < _remaining.Length && IsLeadByte(_remaining[length]))
                {
                    length += _remaining.Length - length >= 2 ? 2 : 1;
                }
            }
            else
            {
                length = 1;
            }

            token = _remaining[..length];
            _remaining = _remaining[length..];
            return true;
        }

        token = default;
        return false;
    }
}
