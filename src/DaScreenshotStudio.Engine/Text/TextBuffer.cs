using System.Buffers.Text;

namespace DaScreenshotStudio.Engine.Text;

public ref struct TextBuffer(Span<byte> destination)
{
    private readonly Span<byte> _destination = destination;
    private int _length;

    public readonly int Length => _length;

    public readonly ReadOnlySpan<byte> Written => _destination[.._length];

    public readonly int Remaining => _destination.Length - _length;

    public void Clear() => _length = 0;

    public void Append(byte value)
    {
        if (_length < _destination.Length)
        {
            _destination[_length++] = value;
        }
    }

    public void Append(char value) => Append((byte)value);

    public void Append(scoped ReadOnlySpan<byte> value)
    {
        var count = Math.Min(value.Length, Remaining);
        value[..count].CopyTo(_destination[_length..]);
        _length += count;
    }

    public void Append(scoped ReadOnlySpan<char> value)
    {
        var count = Math.Min(value.Length, Remaining);
        for (var i = 0; i < count; i++)
        {
            _destination[_length + i] = (byte)value[i];
        }

        _length += count;
    }

    public void Append(int value)
    {
        if (Utf8Formatter.TryFormat(value, _destination[_length..], out var written))
        {
            _length += written;
        }
    }

    public void Append(uint value)
    {
        if (Utf8Formatter.TryFormat(value, _destination[_length..], out var written))
        {
            _length += written;
        }
    }
}
