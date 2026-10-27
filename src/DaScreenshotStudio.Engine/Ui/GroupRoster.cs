namespace DaScreenshotStudio.Engine.Ui;

public sealed class GroupRoster
{
    public const int Capacity = 64;

    public const int NameLength = 0x40;

    public const int PrefixLength = 2;

    public const byte StarMarker = (byte)'*';

    private readonly byte[] _names = new byte[Capacity * NameLength];
    private readonly int[] _lengths = new int[Capacity];
    private readonly bool[] _starred = new bool[Capacity];

    public int Count { get; private set; }

    public int Revision { get; private set; }

    public ReadOnlySpan<byte> NameAt(int index) =>
        (uint)index < (uint)Count ? _names.AsSpan(index * NameLength, _lengths[index]) : default;

    public void Apply(scoped ReadOnlySpan<byte> members)
    {
        Count = 0;
        Revision++;

        var start = -1;

        for (var i = 0; i <= members.Length; i++)
        {
            var value = i < members.Length ? members[i] : (byte)0;

            if (value >= 0x20)
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                Add(members[start..i]);
                start = -1;
            }
        }
    }

    public void Clear()
    {
        Count = 0;
        Revision++;
    }

    public bool Contains(scoped ReadOnlySpan<byte> name)
    {
        for (var i = 0; i < Count; i++)
        {
            if (NameAt(i).SequenceEqual(name))
            {
                return true;
            }
        }

        return false;
    }

    private void Add(scoped ReadOnlySpan<byte> line)
    {
        if (Count >= Capacity || line.Length <= PrefixLength)
        {
            return;
        }

        if (line[1] != (byte)' ' || (line[0] != StarMarker && line[0] != (byte)' '))
        {
            return;
        }

        var starred = line[0] == StarMarker;
        var body = line[PrefixLength..];

        var length = 0;
        while (length < body.Length && length < NameLength)
        {
            var value = body[length];
            if (value is <= 0x20 and <= 0x7F)
            {
                break;
            }

            length++;
        }

        body[..length].CopyTo(_names.AsSpan(Count * NameLength));
        _lengths[Count] = length;
        _starred[Count] = starred;
        Count++;
    }
}
