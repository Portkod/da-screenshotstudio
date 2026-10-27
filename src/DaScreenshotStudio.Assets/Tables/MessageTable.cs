using DaScreenshotStudio.Assets.Fonts;

namespace DaScreenshotStudio.Assets.Tables;

public sealed class MessageTable
{
    public const int Capacity = 129;

    public const byte NewlineMarker = (byte)'^';

    public const int LevelRequirementSlot = 0x78;

    public const int MasterRequirementSlot = 0x79;

    public const int MedenianSlot = 0x7A;

    public const int AbilityRequirementSlot = 0x7B;

    public const int EventProgressionSlot = 0x7C;

    public const int BlockListenPromptSlot = 0x1C;

    public const int BlockListenAddPromptSlot = 0x1D;

    public const int BlockListenRemovePromptSlot = 0x1E;

    public const int DropGoldPromptSlot = 5;

    public const int GiveGoldPromptSlot = 6;

    private readonly byte[] _text;
    private readonly int[] _starts;
    private readonly int[] _lengths;

    private MessageTable(byte[] text, int[] starts, int[] lengths)
    {
        _text = text;
        _starts = starts;
        _lengths = lengths;
    }

    public static MessageTable Empty { get; } = new([], [], []);

    public static MessageTable Parse(ReadOnlySpan<byte> data, LftCodePage codePage)
    {
        if (data.IsEmpty)
        {
            return Empty;
        }

        var text = data.ToArray();
        var starts = new List<int>(Capacity);
        var lengths = new List<int>(Capacity);

        var start = 0;
        var position = 0;

        while (position <= text.Length && starts.Count < Capacity)
        {
            if (position == text.Length)
            {
                if (position > start)
                {
                    starts.Add(start);
                    lengths.Add(position - start);
                }

                break;
            }

            var value = text[position];

            if (codePage.IsLeadByte(value) && position + 1 < text.Length)
            {
                position += 2;
                continue;
            }

            if (value == NewlineMarker)
            {
                text[position] = (byte)'\n';
                position++;
                continue;
            }

            if (value is (byte)'\r' or (byte)'\n')
            {
                var end = position;
                if (value == (byte)'\r' && position + 1 < text.Length && text[position + 1] == (byte)'\n')
                {
                    position++;
                }

                starts.Add(start);
                lengths.Add(end - start);

                position++;
                start = position;
                continue;
            }

            position++;
        }

        return new MessageTable(text, [.. starts], [.. lengths]);
    }

    public ReadOnlySpan<byte> Get(int index) =>
        (uint)index < (uint)_starts.Length ? _text.AsSpan(_starts[index], _lengths[index]) : default;
}
