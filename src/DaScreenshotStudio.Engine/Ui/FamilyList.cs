using DaScreenshotStudio.Engine.App;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class FamilyList
{
    public const string FileName = "Familylist.cfg";

    public const int SlotCount = 20;

    public const int SlotBytes = 40;

    public const int MaxCommittedBytes = 30;

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

    public static FamilyList Load(ClientDirectory? characterDirectory)
    {
        var list = new FamilyList();
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
