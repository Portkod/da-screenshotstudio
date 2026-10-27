using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class EventListPane : CellRollPane
{
    public const string IconEntryName = "leicon.epf";

    public const int IconFrameCount = 3;

    private readonly UiAssets _assets;

    private byte[] _state = [];
    private int[] _payload = [];

    public EventListPane(in PixelRect bounds, PixelBlender blender, UiAssets assets)
        : base(in bounds, blender, assets) => _assets = assets;

    public EventRowState StateAt(int index) => (EventRowState)_state[CheckIndex(index)];

    public int PayloadAt(int index) => _payload[CheckIndex(index)];

    public static int IndexOf(int payload) => payload & 0xFFFF;

    public void SetEvents(EventCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        Grow(category.Count);
        Count = 0;

        for (var i = 0; i < category.Count; i++)
        {
            var slot = RowText(Count);
            category.CopyRow(i, slot[..RecordTextLength], slot[RecordTextLength..], out var title, out var line);

            SetRowLengths(Count, title, line);
            _state[Count] = (byte)category.StateAt(i);
            _payload[Count] = category.PayloadAt(i);
            Count++;
        }

        Position = Math.Min(Position, Maximum);
        Invalidate();
    }

    protected override IconFrame IconFor(int index) =>
        _assets.PlacedPaletteFrame(IconEntryName, _state[index]);

    private void Grow(int rows)
    {
        GrowRows(rows);

        if (_state.Length >= rows)
        {
            return;
        }

        _state = new byte[rows];
        _payload = new int[rows];
    }
}

public sealed class EventCategory
{
    private readonly List<Row> _rows = [];

    public int Count => _rows.Count;

    public EventRowState StateAt(int index) => _rows[index].State;

    public ReadOnlySpan<byte> TitleAt(int index) => _rows[index].Title;

    public ReadOnlySpan<byte> LineAt(int index) => _rows[index].Line;

    public int PayloadAt(int index) => (_rows[index].Table << 16) | _rows[index].Index;

    public void Clear() => _rows.Clear();

    public void AddTable(
        EventTable table,
        int tableNumber,
        MessageTable messages,
        LegendListPane? legend,
        int bucket,
        CharacterClass characterClass)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(messages);

        Span<byte> line = stackalloc byte[EventRoster.ProgressionLineLength];
        for (var index = 0; index < table.Records.Count; index++)
        {
            var record = table.Records[index];
            var written = EventRoster.FormatProgressionLine(record, messages, line);

            _rows.Add(new Row(
                EventRoster.StateOf(record, legend, bucket, characterClass),
                Latin1(record.Title, CellRollPane.RecordTextLength),
                line[..written].ToArray(),
                tableNumber,
                index));
        }
    }

    public void CopyRow(int index, Span<byte> title, Span<byte> line, out int titleLength, out int lineLength)
    {
        var row = _rows[index];
        titleLength = Math.Min(row.Title.Length, title.Length);
        lineLength = Math.Min(row.Line.Length, line.Length);
        row.Title.AsSpan(0, titleLength).CopyTo(title);
        row.Line.AsSpan(0, lineLength).CopyTo(line);
    }

    private static byte[] Latin1(string value, int limit)
    {
        var length = Math.Min(value.Length, limit);
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)value[i];
        }

        return bytes;
    }

    private readonly record struct Row(
        EventRowState State,
        byte[] Title,
        byte[] Line,
        int Table,
        int Index);
}
