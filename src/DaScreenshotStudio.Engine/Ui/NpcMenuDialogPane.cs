using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public enum NpcMenuForm
{
    None,

    List,

    TextInput,

    TextMenu,
}

public sealed class NpcMenuDialogPane : NpcNestedDialogPane
{
    public const string LayoutName = "lnpcd2.txt";

    public static readonly string[] RowArtNames = ["nd_n00.spf", "nd_n01.spf", "nd_n02.spf"];

    public static readonly string[] RowHoverArtNames = ["nd_n00on.spf", "nd_n01on.spf", "nd_n02on.spf"];

    public const int RowGlyphWidth = 6;

    public const int RowLineHeight = 12;

    public const int RowWidthPad = 0x16;

    public const byte RowTextColor = TextPalette.InterfaceIndex;

    public const int RowPressOffset = 1;

    public int CellWidth => Math.Max(1, (_contentRect.Width - _cellReserve) / Columns);

    public const int CellBarReserve = 5;

    public const int CellHeight = 32;

    public int Columns { get; private set; } = DefaultColumns;

    public const int DefaultColumns = 2;

    public const int ValuedColumns = 1;

    public int VisibleCellRows => Math.Max(1, _contentRect.Height / CellHeight);

    public int VisibleCells => VisibleCellRows * Columns;

    public int ListRows => _rowCount == 0 ? 0 : ((_rowCount - 1) / Columns) + 1;

    public int ListMaximum => Math.Clamp(
        (((ListRows * CellHeight) - _contentRect.Height - 1) / CellHeight) + 1, 0, ScrollBarPane.MaximumLimit);

    public int ListPageStep => (VisibleCellRows * Columns) - 1;

    public int ListPosition { get; private set; }

    public const int NameInset = 0x32;

    public const int ValuedNameInset = 0x64;

    public const int ValuedIconInset = 0x1E;

    public const int MaxValueLength = 13;

    public const int ValuedTextTop = 0;

    public const int NameTop = (CellHeight - NameLineHeight) / 2;

    public const int NameLineHeight = 12;

    public const byte SelectedColor = 0x28;

    public const byte RowColor = 0xFF;

    public const int MaxInputLength = byte.MaxValue;

    public const byte PromptColor = TextPalette.InterfaceIndex;

    public const byte InputInkColor = 0x1F;

    public const byte InputBackgroundColor = 0xFF;

    public const int PromptMessageSlot = 0x16;

    private readonly TextBlock _rowText;
    private readonly List<int> _rowOffsets = [];
    private readonly List<IconFrame> _rowIcons = [];
    private readonly List<int> _rowValueOffsets = [];
    private readonly List<int> _rowValueLengths = [];
    private readonly byte[] _rows = new byte[8192];
    private readonly byte[] _rowValues = new byte[byte.MaxValue * MaxValueLength];
    private readonly Pixmap[] _rowArt = new Pixmap[3];
    private readonly Pixmap[] _rowHoverArt = new Pixmap[3];
    private readonly TextEditPane? _input;
    private readonly ImageButtonPane? _ok;
    private readonly GuiTextPane? _prompt;
    private readonly ScrollBarPane? _listBar;

    private readonly int _cellReserve;

    private PixelRect _contentRect;
    private PixelRect _windowRect;
    private PixelRect _menuRowRect;
    private PixelRect _menuRowMinRect;
    private PixelRect _menuRowButtonRect;
    private PixelRect _okRect;
    private int _rowCount;
    private int _rowBytes;
    private int _rowValueBytes;
    private int _selectedRow = -1;
    private int _hoveredRow = -1;
    private int _pressedRow = -1;

    public NpcMenuDialogPane(PixelBlender blender, UiAssets assets)
        : base(assets, blender, LayoutName)
    {
        _ = GuiLayoutGeometry.TryRect(Layout, "Content", out _contentRect);
        var barFrames = ScrollBarPane.LoadFrames(assets);
        _cellReserve = ScrollBarPane.ThicknessOf(barFrames) + CellBarReserve;

        if (barFrames is not null)
        {
            var thickness = ScrollBarPane.ThicknessOf(barFrames);
            _listBar = new ScrollBarPane(
                new PixelRect(_contentRect.Right - thickness, _contentRect.Y, thickness, _contentRect.Height),
                blender,
                barFrames,
                horizontal: false)
            {
                Visible = false,
            };

            _listBar.Scrolled = () => ScrollTo(_listBar.Position);
            Add(_listBar);
        }

        _rowText = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _contentRect.Width), 1)
        {
            Wraps = false,
        };

        for (var i = 0; i < _rowArt.Length; i++)
        {
            _rowArt[i] = assets.Frame(RowArtNames[i], 0);
            _rowHoverArt[i] = assets.Frame(RowHoverArtNames[i], 0);
        }

        _ = GuiLayoutGeometry.TryRect(Layout, "Noname", out _windowRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "TextMenu", out _menuRowRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "TextMenuMin", out _menuRowMinRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "TextMenuButton", out _menuRowButtonRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "Btn1", out _okRect);

        _prompt = AddLabel("ExtraStatic", paletteIndex: PromptColor);
        _input = AddEdit("Text", MaxInputLength);
        _ok = AddButton("Btn1", _ => { });
        Unfocusable(_ok);

        DefaultControl = _ok;

        if (_input is not null)
        {
            _input.Changed = UpdateOkEnabled;

            _input.PaletteIndex = InputInkColor;
            _input.BackgroundIndex = InputBackgroundColor;

            _input.IsTransparent = false;
        }

        _prompt?.SetText(assets.Messages.Get(PromptMessageSlot));
        Begin(NpcMenuForm.None);
    }

    public NpcMenuForm Form { get; private set; }

    public int RowCount => _rowCount;

    public int HoveredRow
    {
        get => _hoveredRow;
        set
        {
            if (_hoveredRow != value)
            {
                _hoveredRow = value;
                Invalidate();
            }
        }
    }

    public int NameWidth => Math.Max(1, CellWidth - NameInset);

    public int ValuedNameWidth(int valueLength) =>
        Math.Max(1, CellWidth - ValuedNameInset - (valueLength * RowGlyphWidth));

    public ReadOnlySpan<byte> RowValue(int index)
    {
        if ((uint)index >= (uint)_rowCount)
        {
            return default;
        }

        return _rowValues.AsSpan(_rowValueOffsets[index], _rowValueLengths[index]);
    }

    public static int FormatValue(uint value, Span<byte> destination)
    {
        if (value == 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        var end = destination.Length;
        var group = 3;

        while (value != 0)
        {
            if (group == 0)
            {
                destination[--end] = (byte)',';
                group = 3;
            }

            destination[--end] = (byte)('0' + (value % 10));
            group--;
            value /= 10;
        }

        var length = destination.Length - end;
        destination[end..].CopyTo(destination);
        return length;
    }

    public ReadOnlySpan<byte> Row(int index)
    {
        if ((uint)index >= (uint)_rowCount)
        {
            return default;
        }

        var start = _rowOffsets[index];
        var end = index + 1 < _rowCount ? _rowOffsets[index + 1] : _rowBytes;
        return _rows.AsSpan(start, end - start);
    }

    public void Begin(NpcMenuForm form, bool valuedRows = false)
    {
        Form = form;
        Columns = valuedRows ? ValuedColumns : DefaultColumns;
        _rowOffsets.Clear();
        _rowIcons.Clear();
        _rowValueOffsets.Clear();
        _rowValueLengths.Clear();
        _rowCount = 0;
        _rowBytes = 0;
        _rowValueBytes = 0;
        _selectedRow = -1;
        _hoveredRow = -1;
        _pressedRow = -1;
        ListPosition = 0;

        Resize();

        if (_listBar is not null)
        {
            _listBar.Visible = form == NpcMenuForm.List;
        }

        RefreshListRange();

        if (_prompt is not null)
        {
            _prompt.Visible = form == NpcMenuForm.TextInput;
        }

        if (_input is not null)
        {
            _input.Clear();
            _input.Visible = form == NpcMenuForm.TextInput;
        }

        if (_ok is not null)
        {
            _ok.Visible = form != NpcMenuForm.None;
        }

        PlaceControls();
        UpdateOkEnabled();

        Visible = form != NpcMenuForm.None;
        Invalidate();
    }

    public void EndRows()
    {
        if (Form != NpcMenuForm.TextMenu)
        {
            return;
        }

        Resize();
        PlaceControls();

        Visible = _rowCount > 0;
        Invalidate();
    }

    public void AddRow(scoped ReadOnlySpan<byte> text, in IconFrame icon) =>
        AddRow(text, icon, valued: false, 0);

    public void AddValuedRow(scoped ReadOnlySpan<byte> text, in IconFrame icon, uint value) =>
        AddRow(text, icon, valued: true, value);

    private void AddRow(scoped ReadOnlySpan<byte> text, in IconFrame icon, bool valued, uint value)
    {
        Span<byte> formatted = stackalloc byte[MaxValueLength];
        var valueLength = valued ? FormatValue(value, formatted) : 0;

        var kept = text.Length;
        var ellipsis = false;

        if (Form == NpcMenuForm.List)
        {
            var width = valued ? ValuedNameWidth(valueLength) : NameWidth;
            kept = Assets.Font.TruncateWithEllipsis(text, width / RowGlyphWidth, out ellipsis);
        }

        var length = kept + (ellipsis ? GameFont.EllipsisLength : 0);
        if (_rowBytes + length > _rows.Length || _rowValueBytes + valueLength > _rowValues.Length)
        {
            return;
        }

        _rowOffsets.Add(_rowBytes);
        _rowIcons.Add(icon);
        _rowValueOffsets.Add(_rowValueBytes);
        _rowValueLengths.Add(valueLength);
        formatted[..valueLength].CopyTo(_rowValues.AsSpan(_rowValueBytes));
        _rowValueBytes += valueLength;
        text[..kept].CopyTo(_rows.AsSpan(_rowBytes));

        if (ellipsis)
        {
            var dots = _rows.AsSpan(_rowBytes + kept, GameFont.EllipsisLength);
            dots[0] = (byte)'.';
            dots[1] = (byte)'.';
            dots[2] = (byte)'.';
        }

        _rowBytes += length;
        _rowCount++;
        RefreshListRange();
        Invalidate();
    }

    private void RefreshListRange()
    {
        if (_listBar is null)
        {
            return;
        }

        _listBar.PageStep = ListPageStep;
        _listBar.Maximum = ListMaximum;

        ListPosition = Math.Clamp(ListPosition, 0, ListMaximum);
        _listBar.Position = ListPosition;
    }

    private void ScrollTo(int row)
    {
        row = Math.Clamp(row, 0, ListMaximum);
        if (row == ListPosition)
        {
            return;
        }

        ListPosition = row;

        if (_listBar is not null)
        {
            _listBar.Position = row;
        }

        Invalidate();
    }

    public void FocusInput()
    {
        if (Form == NpcMenuForm.TextInput && _input is not null)
        {
            Focus(_input);
        }
    }

    public void Close()
    {
        if (Form == NpcMenuForm.None && !Visible)
        {
            return;
        }

        Begin(NpcMenuForm.None);
    }

    public int RowAt(int x, int y)
    {
        if (Form != NpcMenuForm.List || _rowCount == 0)
        {
            return 0;
        }

        var localX = x - _contentRect.X;
        var localY = y - _contentRect.Y;
        if (localX < 0 || localY < 0 || localX >= CellWidth * Columns || localY >= CellHeight * VisibleCellRows)
        {
            return 0;
        }

        var index = (((localY / CellHeight) + ListPosition) * Columns) + (localX / CellWidth);
        return index < _rowCount ? index + 1 : 0;
    }

    public int TextMenuRowAt(int x, int y)
    {
        if (Form != NpcMenuForm.TextMenu)
        {
            return 0;
        }

        for (var i = 0; i < _rowCount; i++)
        {
            if (TextMenuRow(i).Contains(x, y))
            {
                return i + 1;
            }
        }

        return 0;
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        if (Form == NpcMenuForm.TextMenu)
        {
            return TextMenuPointer(in pointer);
        }

        if (pointer.Type == UiEventType.Wheel)
        {
            if (Form != NpcMenuForm.List
                || pointer.Wheel == 0
                || !_contentRect.Contains(pointer.X, pointer.Y))
            {
                return false;
            }

            ScrollTo(ListPosition - pointer.Wheel);
            return true;
        }

        if (pointer.Type != UiEventType.LeftUp)
        {
            return false;
        }

        var row = RowAt(pointer.X, pointer.Y);
        if (row == 0)
        {
            return false;
        }

        _selectedRow = row - 1;
        UpdateOkEnabled();
        Invalidate();
        return true;
    }

    private bool TextMenuPointer(in PointerEvent pointer)
    {
        var row = TextMenuRowAt(pointer.X, pointer.Y) - 1;

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                if (_hoveredRow != row)
                {
                    _hoveredRow = row;
                    Invalidate();
                }

                return false;

            case UiEventType.LeftDown when row >= 0:
                _pressedRow = row;
                Invalidate();
                return true;

            case UiEventType.LeftUp when _pressedRow >= 0:
                _pressedRow = -1;
                Invalidate();
                return true;

            default:
                return false;
        }
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (Visible
            && key.Type == UiEventType.KeyDown
            && key.Key is UiKeys.Enter or UiKeys.Space
            && Form == NpcMenuForm.TextMenu)
        {
            return false;
        }

        return base.OnKey(in key);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!Visible)
        {
            return;
        }

        DrawChrome(canvas);

        switch (Form)
        {
            case NpcMenuForm.List:
                DrawRows(canvas);
                break;

            case NpcMenuForm.TextMenu:
                DrawTextMenuRows(canvas);
                break;

            default:
                break;
        }
    }

    private int ListHeight() =>
        GuiLayoutGeometry.TryRect(Layout, "Noname", out var window) ? window.Height : 194;

    private int TextMenuContentWidth()
    {
        var longest = 0;
        for (var i = 0; i < _rowCount; i++)
        {
            longest = Math.Max(longest, Row(i).Length * RowGlyphWidth);
        }

        longest += _menuRowRect.Width - _menuRowButtonRect.Width + RowWidthPad;
        return Math.Max(_menuRowMinRect.Width, longest);
    }

    private PixelRect TextMenuRow(int index)
    {
        var x = (_menuRowButtonRect.X - _menuRowRect.X) + _contentRect.X;
        var y = (_menuRowButtonRect.Y - _menuRowRect.Y) + _contentRect.Y + (index * _menuRowRect.Height);
        var width = TextMenuContentWidth() - (_menuRowRect.Width - _menuRowButtonRect.Width);

        return new PixelRect(x, y, Math.Max(1, width), _menuRowButtonRect.Height);
    }

    private int InputHeight()
    {
        var height = ListHeight();
        if (GuiLayoutGeometry.TryRect(Layout, "TextInputContent", out var input))
        {
            height -= _contentRect.Height - input.Height;
        }

        return Math.Max(1, height);
    }

    private void PlaceControls()
    {
        if (Form == NpcMenuForm.TextMenu)
        {
            _ok?.SetBounds(new PixelRect(
                _okRect.X + TextMenuContentWidth() - _menuRowRect.Width,
                _okRect.Y + (_rowCount * _menuRowRect.Height) - _contentRect.Height,
                _okRect.Width,
                _okRect.Height));
            return;
        }

        if (!GuiLayoutGeometry.TryRect(Layout, "TextInputContent", out var inputContent))
        {
            return;
        }

        var dx = _contentRect.X - inputContent.X;
        var dy = _contentRect.Y - inputContent.Y;

        Move(_prompt, "ExtraStatic", dx, dy);
        Move(_input, "Text", dx, dy);

        var buttonDy = Form == NpcMenuForm.TextInput ? inputContent.Height - _contentRect.Height : 0;
        Move(_ok, "Btn1", 0, buttonDy);
    }

    private void Move(Pane? control, string controlName, int dx, int dy)
    {
        if (control is null || !GuiLayoutGeometry.TryRect(Layout, controlName, out var rect))
        {
            return;
        }

        control.SetBounds(new PixelRect(rect.X + dx, rect.Y + dy, rect.Width, rect.Height));
    }

    protected override void OnRefreshDefaultActionState() => UpdateOkEnabled();

    private void UpdateOkEnabled()
    {
        if (_ok is null)
        {
            return;
        }

        _ok.Enabled = Form switch
        {
            NpcMenuForm.List => _selectedRow >= 0,
            NpcMenuForm.TextInput => _input is { IsEmpty: false },
            _ => false,
        };
    }

    private void Resize()
    {
        int width;
        int height;

        if (Form == NpcMenuForm.TextMenu)
        {
            width = _windowRect.Width + TextMenuContentWidth() - _menuRowRect.Width;
            height = _windowRect.Height + (_rowCount * _menuRowRect.Height) - _contentRect.Height;
        }
        else
        {
            width = _windowRect.Width;
            height = Form == NpcMenuForm.TextInput ? InputHeight() : ListHeight();
        }

        PlaceAtAnchor(width, height);
    }

    protected override PixelRect ButtonBounds => _ok?.Bounds ?? PixelRect.Empty;

    private void DrawTextMenuRows(Canvas canvas)
    {
        for (var i = 0; i < _rowCount; i++)
        {
            var rect = TextMenuRow(i);
            var art = i == _hoveredRow ? _rowHoverArt : _rowArt;
            var shift = i == _pressedRow ? RowPressOffset : 0;

            canvas.SetClip(rect);

            if (!art[0].IsEmpty && !art[1].IsEmpty && !art[2].IsEmpty)
            {
                canvas.BlitPixmap(rect.X + shift, rect.Y + shift, in art[0], BlendMode.Transparent);

                for (var x = art[0].Width; x + art[2].Width < rect.Width; x += art[1].Width)
                {
                    canvas.BlitPixmap(rect.X + x + shift, rect.Y + shift, in art[1], BlendMode.Transparent);
                }

                canvas.BlitPixmap(
                    rect.X + rect.Width - art[2].Width + shift,
                    rect.Y + shift,
                    in art[2],
                    BlendMode.Transparent);
            }

            var text = Row(i);
            _rowText.DefaultPaletteIndex = RowTextColor;
            _rowText.MaxWidth = Math.Max(1, rect.Width);
            _rowText.SetText(text);
            _rowText.Draw(
                canvas,
                rect.X + ((rect.Width - (text.Length * RowGlyphWidth)) / 2),
                rect.Y + ((rect.Height - RowLineHeight) / 2));

            canvas.ResetClip();
        }
    }

    private void DrawRows(Canvas canvas)
    {
        var first = ListPosition * Columns;
        var last = Math.Min(_rowCount, first + VisibleCells);

        for (var i = first; i < last; i++)
        {
            var cell = i - first;
            var x = _contentRect.X + ((cell % Columns) * CellWidth);
            var y = _contentRect.Y + ((cell / Columns) * CellHeight);

            var valueLength = _rowValueLengths[i];
            var valued = valueLength > 0;

            var icon = _rowIcons[i];
            if (!icon.Image.IsEmpty)
            {
                var iconX = x + (valued ? ValuedIconInset : 0);
                canvas.BlitPixmap(iconX + icon.Left, y + icon.Top, icon.Image, BlendMode.Transparent);
            }

            _rowText.DefaultPaletteIndex = i == _selectedRow ? SelectedColor : RowColor;
            _rowText.MaxWidth = valued ? ValuedNameWidth(valueLength) : NameWidth;
            _rowText.SetText(Row(i));
            _rowText.Draw(
                canvas,
                x + (valued ? ValuedNameInset : NameInset),
                valued ? _contentRect.Y + ValuedTextTop : y + NameTop);

            if (!valued)
            {
                continue;
            }

            var valueWidth = valueLength * RowGlyphWidth;
            _rowText.MaxWidth = valueWidth;
            _rowText.SetText(RowValue(i));
            _rowText.Draw(canvas, x + CellWidth - valueWidth, _contentRect.Y + ValuedTextTop);
        }
    }
}
