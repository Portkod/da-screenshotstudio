namespace DaScreenshotStudio.Engine.Ui;

public sealed class IndexSelection
{
    public const int Capacity = 0x400;

    private readonly int[] _items = new int[Capacity];

    public int Count { get; private set; }

    public ReadOnlySpan<int> Items => _items.AsSpan(0, Count);

    public int IndexOf(int value)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_items[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    public bool Contains(int value) => IndexOf(value) >= 0;

    public bool Add(int value)
    {
        if (Contains(value))
        {
            return true;
        }

        if (Count >= Capacity)
        {
            return false;
        }

        _items[Count++] = value;
        return true;
    }

    public bool Remove(int value)
    {
        var at = IndexOf(value);
        if (at < 0)
        {
            return false;
        }

        _items[at] = _items[Count - 1];
        Count--;
        return true;
    }

    public void Clear() => Count = 0;
}
