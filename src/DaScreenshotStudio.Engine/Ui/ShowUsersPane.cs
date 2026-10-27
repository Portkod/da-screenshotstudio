using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class ShowUsersPane : SlidingWindowPane
{
    public override bool IsMovable => false;

    public const string LayoutName = "_nusers.txt";

    public const string AnchorLayoutName = "lback.txt";

    public const string AnchorControlName = "ShowUserRect";

    public const string CloseControl = "Close";

    public const string ListControl = "UsersList";

    public const string TotalControl = "TotalNum";

    public const string CountControl = "CountryNum";

    public const string FirstButtonControl = "CountryBtn";

    public const string SecondButtonControl = "MasterBtn";

    public const string StateControl = "Emoticon";

    public const int CloseAction = 0;

    public const int FilterCount = 9;

    public const byte CountPaletteIndex = 0xFF;

    public const byte FriendPaletteIndex = 0x80;

    public const byte FamilyPaletteIndex = 0x24;

    public const int RevealOffset = 0xA;

    private readonly PixelRect[] _buttons = new PixelRect[FilterCount];
    private readonly PixelRect[] _counts = new PixelRect[FilterCount];
    private readonly int[] _filterCounts = new int[FilterCount];
    private readonly Pixmap[] _buttonFrames = new Pixmap[FilterCount * 2];
    private readonly TextBlock? _number;
    private readonly PixelRect _totalRect;
    private readonly byte[] _self = new byte[ShowUsersListPane.MaxNameLength];
    private ShowUsersListPane? _list;
    private ScrollBarPane? _bar;
    private FriendList? _friends;
    private FamilyList? _family;
    private int _selfLength;

    private byte[] _rowClass = new byte[64];
    private bool[] _rowGuild = new bool[64];
    private bool[] _rowMaster = new bool[64];
    private byte[] _rowState = new byte[64];
    private byte[] _rowColor = new byte[64];
    private byte[] _rowTitle = new byte[64 * ShowUsersListPane.MaxTitleLength];
    private byte[] _rowTitleLength = new byte[64];
    private byte[] _rowName = new byte[64 * ShowUsersListPane.MaxNameLength];
    private byte[] _rowNameLength = new byte[64];
    private int _rowCount;
    private int _selfRow;
    private PixelRect _resting;

    public ShowUsersPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Placement(assets, LayoutName))
    {
        ConsumesPointerAnywhere = true;
        Visible = false;
        _resting = Bounds;

        Close = AddButton(CloseControl, _ => Answer(CloseAction));

        DefaultControl = Close;

        BuildFilters();
        BuildList(assets, blender);

        _number = new TextBlock(assets.Font, assets.Palette, Math.Max(1, Bounds.Width), 1)
        {
            Wraps = false,
            DefaultPaletteIndex = CountPaletteIndex,
        };

        GuiLayoutGeometry.TryRect(Layout, TotalControl, out _totalRect);
    }

    public ImageButtonPane? Close { get; }

    public ShowUsersListPane? List => _list;

    public ScrollBarPane? ScrollBar => _bar;

    public int WorldCount { get; private set; }

    public int SelectedFilter { get; private set; }

    public int RowCount => _rowCount;

    public bool HasFamilyOrGuild { get; set; }

    public ReadOnlySpan<byte> SelfName => _self.AsSpan(0, _selfLength);

    public void SetSelfName(scoped ReadOnlySpan<byte> name)
    {
        _selfLength = Math.Min(name.Length, _self.Length);
        name[.._selfLength].CopyTo(_self);
    }

    public void Apply(ushort worldCount, ReadOnlySpan<ShowUsersRow> rows, FriendList? friends, FamilyList? family)
    {
        WorldCount = worldCount;
        _rowCount = 0;
        _selfRow = 0;
        Array.Clear(_filterCounts);

        foreach (var row in rows)
        {
            if (row.Title.Length > ShowUsersListPane.MaxTitleLength
                || row.Name.Length > ShowUsersListPane.MaxNameLength)
            {
                continue;
            }

            KeepRow(in row);
        }

        Rebuild(friends, family, preserve: true);
    }

    public void Answer(int action)
    {
        if (action == CloseAction)
        {
            CloseWindow();
            return;
        }

        if (action is < 1 or > FilterCount)
        {
            return;
        }

        SelectedFilter = action - 1;
        Rebuild(_friends, _family, preserve: false);
        Invalidate();
    }

    public void Open()
    {
        BeginSlide(opening: true);
        Visible = true;

        Rebuild(_friends, _family, preserve: false);
        Invalidate();
    }

    public void CloseWindow()
    {
        if (!Visible)
        {
            return;
        }

        BeginSlide(opening: false);
        Invalidate();
        Closed?.Invoke();
    }

    public void Dismiss()
    {
        RestSlide();

        if (!Visible)
        {
            return;
        }

        Visible = false;
        SetBounds(_resting);
        Invalidate();
    }

    protected override PixelRect SlideRest => _resting;

    public Action? Closed { get; set; }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return base.OnKey(in key);
        }

        switch (key.Key)
        {
            case 'E':
                CloseWindow();
                return true;
            case 'Q' or 'R' or 'W' or UiKeys.F7:
                CloseWindow();
                return false;
            default:
                return base.OnKey(in key);
        }
    }

    protected override void OnCancel() => Answer(CloseAction);

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (Visible && pointer.Type == UiEventType.LeftDown)
        {
            for (var i = 0; i < FilterCount; i++)
            {
                if (_buttons[i].Contains(pointer.X, pointer.Y))
                {
                    Answer(i + 1);
                    return true;
                }
            }
        }

        return base.OnPointer(in pointer);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        DrawButtons(canvas);

        if (_number is null)
        {
            return;
        }

        DrawNumber(canvas, WorldCount, in _totalRect);
        for (var i = 0; i < FilterCount; i++)
        {
            DrawNumber(canvas, _filterCounts[i], in _counts[i]);
        }
    }

    private void Rebuild(FriendList? friends, FamilyList? family, bool preserve)
    {
        _friends = friends;
        _family = family;

        if (_list is null)
        {
            return;
        }

        var keep = _list.Position;
        _list.Clear();

        var self = 0;
        for (var i = 0; i < _rowCount; i++)
        {
            if (!InFilter(i, SelectedFilter))
            {
                continue;
            }

            var name = Name(i);
            if (self == 0 && _selfLength > 0 && CharacterNames.EqualsIgnoringCase(name, SelfName))
            {
                self = _list.Count;
            }

            _list.Append(_rowState[i], Resolve(i, friends, family), Title(i), name);
        }

        if (_bar is not null)
        {
            _bar.Maximum = _list.Maximum;
            _bar.PageStep = _list.PageStep;
        }

        _list.Position = keep;
        _selfRow = self;

        if (!preserve)
        {
            var target = self + RevealOffset;
            _list.EnsureVisible(target < _list.Count - 1 ? target : _list.Count - 1);
        }

        if (_bar is not null)
        {
            _bar.Position = _list.Position;
        }

        Invalidate();
    }

    private byte Resolve(int row, FriendList? friends, FamilyList? family)
    {
        var name = Name(row);
        var color = _rowColor[row];

        if (friends is not null && friends.Contains(name))
        {
            color = FriendPaletteIndex;
        }

        if (family is not null && family.Contains(name))
        {
            color = FamilyPaletteIndex;
        }

        return color;
    }

    private bool InFilter(int row, int filter)
    {
        if (filter == 0)
        {
            return true;
        }

        if (filter == 1)
        {
            return _rowMaster[row];
        }

        var byClass = _rowClass[row] == 0 ? 7 : _rowClass[row] + 1;
        if (byClass == filter)
        {
            return true;
        }

        return filter == 8 && HasFamilyOrGuild && _rowGuild[row];
    }

    private void KeepRow(in ShowUsersRow row)
    {
        if (_rowCount == _rowClass.Length)
        {
            GrowRows();
        }

        var index = _rowCount;
        _rowClass[index] = (byte)(row.ClassAndFlags & ShowUsersRow.ClassMask);
        _rowGuild[index] = row.SameGuild;
        _rowMaster[index] = row.IsMaster > 0;
        _rowState[index] = (byte)row.State;
        _rowColor[index] = row.Color;
        _rowTitleLength[index] = (byte)row.Title.Length;
        row.Title.CopyTo(_rowTitle.AsSpan(index * ShowUsersListPane.MaxTitleLength));
        _rowNameLength[index] = (byte)row.Name.Length;
        row.Name.CopyTo(_rowName.AsSpan(index * ShowUsersListPane.MaxNameLength));
        _rowCount++;

        _filterCounts[0]++;
        if (_rowMaster[index])
        {
            _filterCounts[1]++;
        }

        _filterCounts[_rowClass[index] == 0 ? 7 : _rowClass[index] + 1]++;
        if (HasFamilyOrGuild && _rowGuild[index])
        {
            _filterCounts[8]++;
        }
    }

    private ReadOnlySpan<byte> Title(int row) =>
        _rowTitle.AsSpan(row * ShowUsersListPane.MaxTitleLength, _rowTitleLength[row]);

    private ReadOnlySpan<byte> Name(int row) =>
        _rowName.AsSpan(row * ShowUsersListPane.MaxNameLength, _rowNameLength[row]);

    private void GrowRows()
    {
        var wanted = _rowClass.Length * 2;
        Array.Resize(ref _rowClass, wanted);
        Array.Resize(ref _rowGuild, wanted);
        Array.Resize(ref _rowMaster, wanted);
        Array.Resize(ref _rowState, wanted);
        Array.Resize(ref _rowColor, wanted);
        Array.Resize(ref _rowTitle, wanted * ShowUsersListPane.MaxTitleLength);
        Array.Resize(ref _rowTitleLength, wanted);
        Array.Resize(ref _rowName, wanted * ShowUsersListPane.MaxNameLength);
        Array.Resize(ref _rowNameLength, wanted);
    }

    private void DrawButtons(Canvas canvas)
    {
        for (var i = 0; i < FilterCount; i++)
        {
            var frame = _buttonFrames[(i * 2) + (i == SelectedFilter ? 1 : 0)];
            if (!frame.IsEmpty)
            {
                canvas.BlitPixmap(_buttons[i].X, _buttons[i].Y, in frame, BlendMode.Transparent);
            }
        }
    }

    private void DrawNumber(Canvas canvas, int value, in PixelRect rect)
    {
        if (_number is null || rect.IsEmpty)
        {
            return;
        }

        Span<byte> digits = stackalloc byte[12];
        var length = Format(value, digits);

        _number.SetText(digits[..length]);
        var x = rect.Right - (length * ShowUsersListPane.CharacterWidth);
        _number.Draw(canvas, x, rect.Bottom - _number.LineHeight);
    }

    private static int Format(int value, Span<byte> digits)
    {
        if (value <= 0)
        {
            digits[0] = (byte)'0';
            return 1;
        }

        var written = 0;
        while (value > 0)
        {
            digits[written++] = (byte)('0' + (value % 10));
            value /= 10;
        }

        digits[..written].Reverse();
        return written;
    }

    private void BuildFilters()
    {
        if (Layout is null
            || !Layout.TryFind(FirstButtonControl, out var first)
            || !Layout.TryFind(SecondButtonControl, out var second)
            || !GuiLayoutGeometry.TryRect(Layout, CountControl, out var count))
        {
            return;
        }

        var firstRect = GuiLayoutGeometry.Rect(first);
        var secondRect = GuiLayoutGeometry.Rect(second);

        var pitch = secondRect.Y - firstRect.Y;

        for (var i = 0; i < FilterCount; i++)
        {
            _buttons[i] = new PixelRect(firstRect.X, firstRect.Y + (i * pitch), firstRect.Width, firstRect.Height);
            _counts[i] = new PixelRect(count.X, count.Y + (i * pitch), count.Width, count.Height);
        }

        if (first.Images.Count < 2 || second.Images.Count < 2)
        {
            return;
        }

        var entry = first.Images[1].EntryName;
        var selectedBase = first.Images[1].Frame;
        var step = second.Images[1].Frame - selectedBase;
        for (var i = 0; i < FilterCount; i++)
        {
            _buttonFrames[i * 2] = Assets.LayoutFrame(entry, selectedBase + (step * i) - 1);
            _buttonFrames[(i * 2) + 1] = Assets.LayoutFrame(entry, selectedBase + (step * i));
        }
    }

    private void BuildList(UiAssets assets, PixelBlender blender)
    {
        if (!TryBuildScrolledControl(
                ListControl,
                content => new ShowUsersListPane(in content, blender, assets),
                out var list,
                out var bar))
        {
            return;
        }

        if (Layout is not null && Layout.TryFind(StateControl, out var emoticon))
        {
            list.SetStateFrames(assets, emoticon);
        }

        _list = list;
        _bar = bar;
    }

    private static PixelRect Placement(UiAssets assets, string layoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var layout = assets.Layout(layoutName);
        if (layout is not { ControlCount: > 0 })
        {
            return PixelRect.Empty;
        }

        var window = GuiLayoutGeometry.Rect(layout[0]);
        if (!GuiLayoutGeometry.TryRect(assets.Layout(AnchorLayoutName), AnchorControlName, out var anchor))
        {
            return window;
        }

        return new PixelRect(anchor.Right - window.Width, anchor.Y, window.Width, window.Height);
    }
}
