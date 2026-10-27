using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record EventEntry(
    string Id,
    string Title,
    int LevelBands,
    int Classes,
    IReadOnlyList<string> Summary,
    IReadOnlyList<string> Result,
    string Reward = "")
{
    public static int TierOf(int level, bool secondState, bool thirdState) =>
        thirdState ? 7
        : secondState ? 6
        : level >= 0x63 ? 5
        : level >= 0x47 ? 4
        : level >= 0x29 ? 3
        : level >= 0x0B ? 2
        : 1;

    public bool Qualifies(int tier, int playerClass) =>
        (LevelBands & (1 << (tier - 1))) != 0 && (Classes & (1 << (playerClass - 1))) != 0;
}

public sealed record EventRow(string Title, IReadOnlyList<string> Lines, int IconFrame)
{
    public const int LineCount = 4;

    public const int FieldLength = 0x4E;
}

public sealed class EventViewPane : LayoutPane
{
    public const string LayoutName = "llevent.txt";

    public const string BackgroundControl = "Noname";

    public const string RowControl = "EventBase";

    public const string ListControl = "List";

    public const string IconControl = "EventIcon";

    public const string TitleControl = "EventTitle";

    public const string DescriptionControl = "EventDesc";

    public const int KnownFrame = 0;

    public const int OpenFrame = 1;

    public const int ClosedFrame = 2;

    public const int DescriptionLineHeight = 0xC;

    public const byte TitleInk = 0x14;

    private readonly PixelRect _rowRect;
    private readonly PixelRect _listRect;
    private readonly PixelRect _iconRect;
    private readonly PixelRect _titleRect;
    private readonly PixelRect _descriptionRect;
    private readonly Pixmap _plate;
    private readonly Pixmap[] _icons = new Pixmap[3];
    private readonly TextBlock _text;

    private readonly List<EventRow> _rows = [];
    private readonly ScrollBarPane? _bar;
    protected override ScrollBarPane? WheelScrollBar(int index) => _bar;

    protected override PixelRect WheelControlArea(int index) => _listRect;
    private int _top;

    public EventViewPane(UiAssets assets, PixelBlender blender, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        GuiLayoutGeometry.TryRect(Layout, RowControl, out _rowRect);
        ConsumesPointerInside = true;
        GuiLayoutGeometry.TryRect(Layout, ListControl, out _listRect);

        _plate = ControlFrame(RowControl, 0);
        for (var i = 0; i < _icons.Length; i++)
        {
            _icons[i] = ControlFrame(IconControl, i);
        }

        _iconRect = Rebased(assets, IconControl);
        _titleRect = Rebased(assets, TitleControl);
        _descriptionRect = Rebased(assets, DescriptionControl);

        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _descriptionRect.Width), 1)
        {
            DefaultPaletteIndex = TitleInk,
        };

        if (ScrollBarPane.LoadFrames(assets) is { } frames)
        {
            var thickness = ScrollBarPane.ThicknessOf(frames);
            _bar = new ScrollBarPane(
                new PixelRect(_listRect.Right - thickness, _listRect.Y, thickness, _listRect.Height),
                blender,
                frames,
                horizontal: false);

            var bar = _bar;
            _bar.Scrolled = () => Top = bar.Position;
            Add(_bar);
        }
    }

    protected override bool OverOwnControl(in PointerEvent pointer) =>
        _listRect.Contains(pointer.X, pointer.Y);

    public IReadOnlyList<EventRow> Rows => _rows;

    public ScrollBarPane? Bar => _bar;

    public int Top
    {
        get => _top;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, _rows.Count - VisibleRows));
            if (_top == clamped)
            {
                return;
            }

            _top = clamped;
            if (_bar is not null)
            {
                _bar.Position = _top;
            }

            Invalidate();
        }
    }

    public int VisibleRows => Math.Max(1, _listRect.Height / Math.Max(1, _rowRect.Height));

    public PixelRect Cell => new(0, 0, _rowRect.Width, _rowRect.Height);

    public static PixelRect Placement(UiAssets assets) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, BackgroundControl);

    public static int FrameFor(bool known, bool open) =>
        known ? KnownFrame : open ? OpenFrame : ClosedFrame;

    public static EventRow RowFor(EventEntry entry, bool known, bool open)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var source = known ? entry.Summary : entry.Result;
        var lines = new List<string>(EventRow.LineCount);

        for (var i = 0; i < EventRow.LineCount; i++)
        {
            var line = i < source.Count ? source[i] : string.Empty;
            lines.Add(line.Length > EventRow.FieldLength ? line[..EventRow.FieldLength] : line);
        }

        var title = entry.Title.Length > EventRow.FieldLength
            ? entry.Title[..EventRow.FieldLength]
            : entry.Title;

        return new EventRow(title, lines, FrameFor(known, open));
    }

    public void SetRows(IEnumerable<EventRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var position = _top;
        _rows.Clear();
        _rows.AddRange(rows);
        _top = Math.Clamp(position, 0, Math.Max(0, _rows.Count - VisibleRows));
        if (_bar is not null)
        {
            _bar.Maximum = Math.Max(0, _rows.Count - VisibleRows);
            _bar.Position = _top;
        }

        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        for (var i = _top; i < _rows.Count; i++)
        {
            var origin = _listRect.Y + ((i - _top) * _rowRect.Height);
            if (origin >= _listRect.Bottom)
            {
                break;
            }

            DrawRow(canvas, _rows[i], _listRect.X, origin);
        }
    }

    private void DrawRow(Canvas canvas, EventRow row, int x, int y)
    {
        if (!_plate.IsEmpty)
        {
            canvas.BlitPixmap(x, y, in _plate, BlendMode.Transparent);
        }

        var icon = _icons[Math.Clamp(row.IconFrame, 0, _icons.Length - 1)];
        if (!icon.IsEmpty)
        {
            canvas.BlitPixmap(x + _iconRect.X, y + _iconRect.Y, in icon, BlendMode.Transparent);
        }

        _text.MaxWidth = Math.Max(1, _titleRect.Width);
        _text.SetText(System.Text.Encoding.Latin1.GetBytes(row.Title));
        _text.Draw(canvas, x + _titleRect.X, y + _titleRect.Y);

        _text.MaxWidth = Math.Max(1, _descriptionRect.Width);
        for (var j = 0; j < row.Lines.Count; j++)
        {
            _text.SetText(System.Text.Encoding.Latin1.GetBytes(row.Lines[j]));
            _text.Draw(canvas, x + _descriptionRect.X, y + _descriptionRect.Y + (j * DescriptionLineHeight));
        }
    }

    private PixelRect Rebased(UiAssets assets, string control)
    {
        GuiLayoutGeometry.TryRect(Layout, control, out var rect);
        return new PixelRect(rect.X - _rowRect.X, rect.Y - _rowRect.Y, rect.Width, rect.Height);
    }
}

public sealed class EventInfoPane : SessionLayeredTabPane
{
    public const string LayoutName = "llinfo.txt";

    public const string TabStripControl = "BTab";

    public const string ContentControl = "Content";

    public const int PageCount = 7;

    public const int TabImageIndex = 0;

    public const int FirstMessageId = 0x00100001;

    private readonly EventViewPane _view;
    private readonly List<EventEntry>[] _tabs =
    [
        [], [], [], [], [], [], [],
    ];

    public EventInfoPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Placement(assets, TabStripControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        var column = Placement(assets, TabStripControl);

        for (var i = 0; i < PageCount; i++)
        {
            var tab = $"BTab{i}";
            GuiLayoutGeometry.TryRect(Layout, tab, out var tabRect);
            GuiLayoutGeometry.TryRect(Layout, $"{tab}R", out var stripRect);

            var art = ControlFrame(tab, TabImageIndex);
            AddTab(
                new PixelRect(tabRect.X - column.X, tabRect.Y - column.Y, tabRect.Width, tabRect.Height),
                art,
                art,
                default,
                in stripRect,
                ControlFrame($"{tab}R", TabImageIndex));
        }

        _view = new EventViewPane(assets, blender, Placement(assets, ContentControl))
        {
            Visible = false,
        };
    }

    public int PlayerLevel { get; set; }

    public int PlayerClass { get; set; }

    public bool SecondState { get; set; }

    public bool ThirdState { get; set; }

    public EventViewPane View => _view;

    public static PixelRect Placement(UiAssets assets, string control) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, control);

    public void SetTab(int tab, IEnumerable<EventEntry> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if ((uint)tab >= (uint)_tabs.Length)
        {
            return;
        }

        _tabs[tab].Clear();
        _tabs[tab].AddRange(events);

        if (tab == CurrentPage)
        {
            Feed(tab);
        }
    }

    public void Feed(int tab)
    {
        if ((uint)tab >= (uint)_tabs.Length)
        {
            return;
        }

        var tier = EventEntry.TierOf(PlayerLevel, SecondState, ThirdState);
        var rows = new List<EventRow>(_tabs[tab].Count);

        foreach (var entry in _tabs[tab])
        {
            var known = false;
            rows.Add(EventViewPane.RowFor(entry, known, entry.Qualifies(tier, PlayerClass)));
        }

        _view.SetRows(rows);
    }

    public void Show()
    {
        Visible = true;
        _view.Visible = true;
        Select(0);
        Invalidate();
    }

    public void Hide()
    {
        LeaveCurrentPage();
        _view.Visible = false;
        Visible = false;
        Invalidate();
    }

    protected override void OnPageEnter(int index) => Feed(index);

    protected override void OnPageLeave(int index)
    {
    }
}
