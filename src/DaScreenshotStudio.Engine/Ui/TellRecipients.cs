namespace DaScreenshotStudio.Engine.Ui;

public sealed class TellRecipients
{
    public const int Capacity = 10;

    public const int MaxNameBytes = 12;

    private readonly byte[] _names = new byte[Capacity * MaxNameBytes];
    private readonly int[] _lengths = new int[Capacity];
    private int _cursor;

    public ReadOnlySpan<byte> MostRecent => this[0];

    public ReadOnlySpan<byte> this[int index] =>
        (uint)index < Capacity ? _names.AsSpan(index * MaxNameBytes, _lengths[index]) : default;

    public void Remember(scoped ReadOnlySpan<byte> name)
    {
        if (name.IsEmpty)
        {
            return;
        }

        if (name.Length > MaxNameBytes)
        {
            name = name[..MaxNameBytes];
        }

        var slot = IndexOf(name);
        if (slot < 0)
        {
            slot = Capacity - 1;
        }

        for (var i = slot; i > 0; i--)
        {
            _names.AsSpan((i - 1) * MaxNameBytes, MaxNameBytes).CopyTo(_names.AsSpan(i * MaxNameBytes));
            _lengths[i] = _lengths[i - 1];
        }

        name.CopyTo(_names.AsSpan(0, MaxNameBytes));
        _lengths[0] = name.Length;
        _cursor = 0;
    }

    public bool TryOlder(out ReadOnlySpan<byte> name)
    {
        _cursor = (_cursor + 1) % Capacity;

        if (_lengths[_cursor] == 0)
        {
            _cursor = 0;
        }

        name = this[_cursor];
        return !name.IsEmpty;
    }

    public bool TryNewer(out ReadOnlySpan<byte> name)
    {
        var cursor = _cursor == 0 ? Capacity - 1 : _cursor - 1;

        while (cursor >= 0 && _lengths[cursor] == 0)
        {
            cursor--;
        }

        if (cursor < 0)
        {
            _cursor = 0;
            name = default;
            return false;
        }

        _cursor = cursor;
        name = this[cursor];
        return true;
    }

    public void ResetCursor() => _cursor = 0;

    private int IndexOf(scoped ReadOnlySpan<byte> name)
    {
        for (var i = 0; i < Capacity; i++)
        {
            if (_lengths[i] == name.Length && name.SequenceEqual(this[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
