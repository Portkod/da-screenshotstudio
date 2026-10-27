using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class FriendList
{
    public const string FileName = "Friendlist.cfg";

    public const int SlotCount = 20;

    public const int SlotBytes = 40;

    public const int MaxCommittedBytes = SlotBytes - 1;

    private readonly byte[] _slots = new byte[SlotCount * SlotBytes];
    private readonly int[] _lengths = new int[SlotCount];

    public ReadOnlySpan<byte> this[int slot] =>
        (uint)slot < (uint)SlotCount ? _slots.AsSpan(slot * SlotBytes, _lengths[slot]) : default;

    public bool Contains(scoped ReadOnlySpan<byte> name)
    {
        for (var slot = 0; slot < SlotCount; slot++)
        {
            var stored = this[slot];
            if (stored.Length != 0 && CharacterNames.EqualsIgnoringCase(stored, name))
            {
                return true;
            }
        }

        return false;
    }

    public void SetSlot(int slot, scoped ReadOnlySpan<byte> value)
    {
        if ((uint)slot >= (uint)SlotCount)
        {
            return;
        }

        var length = Math.Min(value.Length, MaxCommittedBytes);
        if (length == _lengths[slot] && value[..length].SequenceEqual(this[slot]))
        {
            return;
        }

        value[..length].CopyTo(_slots.AsSpan(slot * SlotBytes, SlotBytes));
        _slots.AsSpan((slot * SlotBytes) + length, SlotBytes - length).Clear();
        _lengths[slot] = length;
    }

    public static FriendList Load(ClientDirectory? characterDirectory)
    {
        var list = new FriendList();
        if (characterDirectory is null || !characterDirectory.TryRent(FileName, out var file))
        {
            return list;
        }

        var read = 0;
        var start = 0;
        var span = file.Span;
        while (read < SlotCount && start <= span.Length)
        {
            var end = span[start..].IndexOf((byte)'\n');
            var line = end < 0 ? span[start..] : span.Slice(start, end);
            if (line.Length > 0 && line[^1] == (byte)'\r')
            {
                line = line[..^1];
            }

            list.KeepLine(read++, line);

            if (end < 0)
            {
                break;
            }

            start += end + 1;
        }

        file.Dispose();
        return list;
    }

    private void KeepLine(int slot, scoped ReadOnlySpan<byte> line)
    {
        var length = Math.Min(line.Length, SlotBytes - 1);
        line[..length].CopyTo(_slots.AsSpan(slot * SlotBytes, SlotBytes));
        _lengths[slot] = length;
    }
}

internal static class CharacterNames
{
    public static bool EqualsIgnoringCase(scoped ReadOnlySpan<byte> left, scoped ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (Fold(left[i]) != Fold(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte Fold(byte value) =>
        value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + ('a' - 'A')) : value;
}
