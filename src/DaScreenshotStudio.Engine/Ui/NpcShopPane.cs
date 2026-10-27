using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Records;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class NpcShopPane : LayoutPane
{
    public const int RowsPerPage = 4;

    public const int GlyphWidth = 6;

    public const int LineHeight = 12;

    public const int VisibleTabs = 4;

    public const byte TextColor = 0xFF;

    public const byte SelectedTextColor = 0x28;

    public const int OtherCategorySlot = 100;

    public const int ClassNameSlot = 0x5E;

    public const int ClassNameCount = 6;

    public const int NoLevelLimitSlot = 0x66;

    public const int MasterLevelSlot = 0x67;

    public const int DescriptionLine = 3;

    public const int TooltipOffsetX = DescPane.PointerOffsetX;

    public const int TooltipOffsetY = DescPane.PointerOffsetY;

    private readonly TextBlock _text;
    private readonly TextBlock _wrapped;
    private readonly List<ItemRow> _rows = [];
    private readonly byte[] _names = new byte[16384];
    private readonly byte[] _categoryText = new byte[4096];
    private readonly byte[] _detail = new byte[1024];
    private readonly TextSpan[] _detailLines = new TextSpan[4];
    private readonly byte[] _tooltipText = new byte[256];
    private readonly byte[] _digits = new byte[24];
    private readonly byte[] _scratch = new byte[256];
    private ImageButtonPane? _ok;
    private ImageButtonPane? _tabPrev;
    private ImageButtonPane? _tabNext;
    private ImageButtonPane? _pagePrev;
    private ImageButtonPane? _pageNext;
    private Pixmap _tabNormal;
    private Pixmap _tabSelected;
    private PixelRect _contentRect;
    private PixelRect _iconRect;
    private PixelRect _nameRect;
    private PixelRect _moneyRect;
    private PixelRect _pageRect;
    private PixelRect _tabLabelRect;
    private readonly PixelRect[] _tabRects = new PixelRect[VisibleTabs];
    private readonly PixelRect[] _descRects = new PixelRect[4];
    private int _nameBytes;
    private int _categoryBytes;
    private int _detailBytes;
    private uint _money;
    private int _category;
    private int _page = 1;
    private int _selectedRow = -1;

    private const int NoOwnControl = -1;

    private const int RowGridControl = -2;

    private int _pressedOwnControl = NoOwnControl;

    private int _detailRow = -1;

    public TooltipHandler? TooltipChanged { get; set; }

    public delegate void TooltipHandler(ReadOnlySpan<byte> text, int screenX, int screenY);

    public ReadOnlySpan<byte> DetailLine(int index) => (uint)index < 4
        ? _detail.AsSpan(_detailLines[index].Start, _detailLines[index].Length)
        : default;

    public int RowAt(int x, int y)
    {
        for (var i = 0; i < VisibleRowCount; i++)
        {
            if (RowBounds(i).Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    private int OwnControlAt(int x, int y) =>
        TabSlotAt(x, y) is var slot && slot >= 0
            ? slot
            : _contentRect.Contains(x, y) ? RowGridControl : NoOwnControl;

    public int TabSlotAt(int x, int y)
    {
        for (var i = 0; i < _tabRects.Length; i++)
        {
            if (_tabRects[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    protected override bool OverOwnControl(in PointerEvent pointer)
    {
        if (_contentRect.Contains(pointer.X, pointer.Y))
        {
            return true;
        }

        if (TabSlotAt(pointer.X, pointer.Y) >= 0)
        {
            return true;
        }

        for (var i = 0; i < _descRects.Length; i++)
        {
            if (_descRects[i].Contains(pointer.X, pointer.Y))
            {
                return true;
            }
        }

        return false;
    }

    private ItemRow NewRow(in ShopItem item)
    {
        var row = default(ItemRow);
        row.Name = Store(_names, ref _nameBytes, item.Name);
        row.Sprite = item.Sprite;
        row.Color = item.Color;
        row.Value = item.Value;
        row.Quantity = item.Quantity;
        row.HasQuantity = item.HasQuantity;
        row.ProgressCurrent = item.ProgressCurrent;
        row.ProgressLimit = item.ProgressLimit;
        row.Icon = Assets.ItemIcons?.Frame(item.Sprite, item.Sprite > IconArt.ItemTag ? item.Color : (byte)0) ?? default;
        row.Info = Lookup(item.Name);
        return row;
    }

    private bool TryRow(int visibleIndex, out ItemRow row)
    {
        var index = RowIndex(visibleIndex);
        if (index < 0)
        {
            row = default;
            return false;
        }

        row = _rows[index];
        return true;
    }

    private void ShowDetail(int visibleIndex)
    {
        _detailRow = RowIndex(visibleIndex);
        _detailBytes = 0;
        _detailLines.AsSpan().Clear();
        Invalidate();

        if (!TryRow(visibleIndex, out var row) || row.Info is not { } info)
        {
            return;
        }

        var category = (uint)info.Category < ClassNameCount ? info.Category : 0;
        _detailLines[0] = Store(_detail, ref _detailBytes, Assets.Messages.Get(ClassNameSlot + category));
        _detailLines[1] = Store(_detail, ref _detailBytes, LevelLine(info));

        Span<byte> weight = stackalloc byte[16];
        _detailLines[2] = Store(_detail, ref _detailBytes, weight[..Digits(info.NumericValue, weight)]);
        _detailLines[3] = Store(_detail, ref _detailBytes, Ascii(info.Detail));
    }

    private ItemInfoRecord? Lookup(scoped ReadOnlySpan<byte> name)
    {
        Span<char> text = stackalloc char[Math.Min(name.Length, 128)];
        for (var i = 0; i < text.Length; i++)
        {
            text[i] = (char)name[i];
        }

        return Assets.ItemInfo.Find(text);
    }

    private ReadOnlySpan<byte> Ascii(string value)
    {
        var length = Math.Min(value.Length, _scratch.Length);
        for (var i = 0; i < length; i++)
        {
            _scratch[i] = (byte)value[i];
        }

        return _scratch.AsSpan(0, length);
    }

    private static TextSpan Store(byte[] destination, ref int used, scoped ReadOnlySpan<byte> value)
    {
        var length = Math.Min(value.Length, destination.Length - used);
        value[..length].CopyTo(destination.AsSpan(used));
        var span = new TextSpan(used, length);
        used += length;
        return span;
    }

    private ReadOnlySpan<byte> Tooltip(int visibleIndex)
    {
        if (!TryRow(visibleIndex, out var row))
        {
            return default;
        }

        var text = new TextBuffer(_tooltipText);
        text.Append(DescPane.NamePrefix);
        text.Append(_names.AsSpan(row.Name.Start, row.Name.Length));

        if (row.HasQuantity
            && row.ProgressCurrent > 0
            && row.ProgressLimit != ShopItem.NoProgressLimit)
        {
            text.Append(DescPane.DurabilityPrefix);
            text.Append(row.ProgressCurrent);
            text.Append(DescPane.DurabilitySeparator);
            text.Append(row.ProgressLimit);
        }

        return _tooltipText.AsSpan(0, text.Length);
    }

    private ReadOnlySpan<byte> LevelLine(ItemInfoRecord info)
    {
        if (info.RangeMax <= 0)
        {
            return Assets.Messages.Get(NoLevelLimitSlot);
        }

        if (info.RangeMin < 0)
        {
            return _scratch.AsSpan(0, Digits(info.RangeMax, _scratch));
        }

        if (info.RangeMax == 99 && info.RangeMin == 1)
        {
            return Assets.Messages.Get(MasterLevelSlot);
        }

        var length = Digits(info.RangeMax, _scratch);
        _scratch[length++] = (byte)' ';
        _scratch[length++] = (byte)'-';
        _scratch[length++] = (byte)' ';
        length += Digits(info.RangeMin, _scratch.AsSpan(length));
        return _scratch.AsSpan(0, length);
    }

    private void DrawMoney(Canvas canvas)
    {
        var length = FormatNumber(_money);
        _text.MaxWidth = Math.Max(1, length * GlyphWidth);
        _text.SetText(_digits.AsSpan(0, length));
        _text.Draw(canvas, _moneyRect.Right - (length * GlyphWidth), _moneyRect.Bottom - LineHeight);
    }

    private static int Digits(int value, Span<byte> destination)
    {
        if (value <= 0)
        {
            destination[0] = (byte)'0';
            return 1;
        }

        Span<byte> reversed = stackalloc byte[12];
        var count = 0;
        while (value > 0 && count < reversed.Length)
        {
            reversed[count++] = (byte)('0' + (value % 10));
            value /= 10;
        }

        for (var i = 0; i < count; i++)
        {
            destination[i] = reversed[count - 1 - i];
        }

        return count;
    }

    private int FormatNumber(uint value)
    {
        if (value == 0)
        {
            _digits[0] = (byte)'0';
            return 1;
        }

        var end = _digits.Length;
        var group = 3;

        while (value > 0 && end > 0)
        {
            if (group == 0)
            {
                _digits[--end] = (byte)',';
                group = 3;
            }

            _digits[--end] = (byte)('0' + (value % 10));
            value /= 10;
            group--;
        }

        var length = _digits.Length - end;
        _digits.AsSpan(end, length).CopyTo(_digits);
        return length;
    }

    private readonly record struct TextSpan(int Start, int Length);

    private struct ItemRow
    {
        public TextSpan Name;
        public ushort Sprite;
        public byte Color;
        public uint Value;
        public byte Quantity;
        public bool HasQuantity;
        public uint ProgressCurrent;
        public uint ProgressLimit;
        public int Category;
        public ItemInfoRecord? Info;
        public IconFrame Icon;
    }

    public const string LayoutName = "lnpcd3.txt";

    public int RowPitch { get; private set; }

    public static ReadOnlySpan<byte> SoldOutMarker => [0x5B, 0xB8, 0xC5, 0xC1, 0xF8, 0x5D];

    private readonly List<int> _visible = [];
    private readonly List<TextSpan> _categories = [];
    private Pixmap _background;
    private int _tabOrigin;
    private int _pageCount = 1;
    private int _hoverRow = -1;

    public NpcShopPane(PixelBlender blender, UiAssets assets)
        : base(assets, blender, LayoutName, Anchored(assets))
    {
        _text = new TextBlock(assets.Font, assets.Palette, 1, 1)
        {
            Wraps = false,
            DefaultPaletteIndex = TextColor,
        };

        _wrapped = new TextBlock(assets.Font, assets.Palette, 1, 6)
        {
            DefaultPaletteIndex = TextColor,
        };
        Visible = false;

        ConsumesPointerInside = true;

        ReadLayout();

        _ok = AddButton("Btn1", _ => { });
        DefaultControl = _ok;
        _tabPrev = AddButton("TabPrev", _ => ScrollTabs(-VisibleTabs));
        _tabNext = AddButton("TabNext", _ => ScrollTabs(VisibleTabs));
        _pagePrev = AddButton("PagePrev", _ => SetPage(_page - 1));
        _pageNext = AddButton("PageNext", _ => SetPage(_page + 1));

        Unfocusable(_ok);
        Unfocusable(_tabPrev);
        Unfocusable(_tabNext);
        Unfocusable(_pagePrev);
        Unfocusable(_pageNext);
        UpdateButtons();
    }

    public int VisibleRowCount =>
        Math.Clamp(_visible.Count - ((_page - 1) * RowsPerPage), 0, RowsPerPage);

    public void Show(IReadOnlyList<ShopItem> items, uint money)
    {
        ArgumentNullException.ThrowIfNull(items);

        _money = money;
        _rows.Clear();
        _visible.Clear();
        _categories.Clear();
        _nameBytes = 0;
        _categoryBytes = 0;
        _detailBytes = 0;
        _detailLines.AsSpan().Clear();
        _selectedRow = -1;
        _detailRow = -1;
        Hover(-1, 0, 0);

        foreach (var item in items)
        {
            var row = NewRow(in item);
            row.Category = CategoryOf(row.Info);
            _rows.Add(row);
        }

        _tabOrigin = 0;
        SelectCategory(0);

        Visible = true;
        Invalidate();
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        _rows.Clear();
        _visible.Clear();
        _categories.Clear();

        Hover(-1, 0, 0);
        Invalidate();
    }

    public ReadOnlySpan<byte> CategoryName(int index) => (uint)index < (uint)_categories.Count
        ? _categoryText.AsSpan(_categories[index].Start, _categories[index].Length)
        : default;

    public void SelectCategory(int index)
    {
        _category = _categories.Count == 0 ? 0 : Math.Clamp(index, 0, _categories.Count - 1);
        _visible.Clear();

        for (var i = 0; i < _rows.Count; i++)
        {
            if (_categories.Count == 0 || _rows[i].Category == _category)
            {
                _visible.Add(i);
            }
        }

        _pageCount = Math.Max(1, ((Math.Max(1, _visible.Count) - 1) / RowsPerPage) + 1);
        _selectedRow = -1;
        SetPage(1);
    }

    public void ScrollTabs(int delta)
    {
        var last = Math.Max(0, _categories.Count - VisibleTabs);
        _tabOrigin = Math.Clamp(_tabOrigin + delta, 0, last);
        UpdateButtons();
        Invalidate();
    }

    public void SetPage(int page)
    {
        _page = Math.Clamp(page, 1, _pageCount);
        _selectedRow = -1;
        UpdateButtons();
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return base.OnPointer(in pointer);
        }

        var row = RowAt(pointer.X, pointer.Y);

        switch (pointer.Type)
        {
            case UiEventType.PointerMove:
                Hover(row, pointer.X, pointer.Y);
                break;

            case UiEventType.LeftDown:
                _pressedOwnControl = OwnControlAt(pointer.X, pointer.Y);
                break;

            case UiEventType.LeftUp when row >= 0 && _pressedOwnControl == RowGridControl:
                _selectedRow = row;
                UpdateButtons();
                Invalidate();
                break;

            case UiEventType.LeftUp
                when TabAt(pointer.X, pointer.Y) is var tab && tab >= 0 && tab == _pressedOwnControl:
                SelectCategory(CategoryInSlot(tab));
                break;

            default:
                break;
        }

        if (pointer.Type == UiEventType.LeftUp)
        {
            _pressedOwnControl = NoOwnControl;
        }

        return base.OnPointer(in pointer);
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (key.Key == UiKeys.Escape)
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

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Copy);
        }

        DrawTabs(canvas);
        DrawRows(canvas);
        DrawDetail(canvas);
        DrawMoney(canvas);
        DrawPage(canvas);
    }

    private PixelRect RowBounds(int index) =>
        new(_contentRect.X, _contentRect.Y + (index * RowPitch), _contentRect.Width, RowPitch);

    private static PixelRect Anchored(UiAssets assets)
    {
        var rect = GuiLayoutGeometry.TryRect(assets?.Layout(LayoutName), "Noname", out var window)
            ? window
            : new PixelRect(0, 0, 449, 292);

        var bar = NpcDialogPane.MessageBarRect(assets!);
        return new PixelRect(bar.Right - rect.Width, bar.Y - rect.Height, rect.Width, rect.Height);
    }

    private void ReadLayout()
    {
        _ = GuiLayoutGeometry.TryRect(Layout, "Content", out _contentRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "ItemIcon1", out _iconRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "ItemName1", out _nameRect);

        RowPitch = GuiLayoutGeometry.TryRect(Layout, "ItemIcon2", out var secondIcon)
            ? Math.Max(1, secondIcon.Y - _iconRect.Y)
            : 1;

        _ = GuiLayoutGeometry.TryRect(Layout, "Money", out _moneyRect);
        _ = GuiLayoutGeometry.TryRect(Layout, "Page", out _pageRect);

        _ = GuiLayoutGeometry.TryRect(Layout, "Tab0", out _tabRects[0]);
        _ = GuiLayoutGeometry.TryRect(Layout, "Tab1", out _tabRects[1]);
        var step = _tabRects[1].X - _tabRects[0].X;
        for (var i = 2; i < VisibleTabs; i++)
        {
            var previous = _tabRects[i - 1];
            _tabRects[i] = new PixelRect(previous.X + step, previous.Y, previous.Width, previous.Height);
        }

        _tabLabelRect = new PixelRect(4, 4, Math.Max(1, _tabRects[0].Width - 8), LineHeight);

        ReadDetailRect(0, "DescClass");
        ReadDetailRect(1, "DescLevel");
        ReadDetailRect(2, "DescWeight");
        ReadDetailRect(3, "DescText");

        if (Layout is not null && Layout.TryFind("Noname", out var window) && window.Images.Count > 0)
        {
            _background = Assets.Frame(window.Images[0].EntryName, window.Images[0].Frame);
        }

        if (Layout is not null && Layout.TryFind("Tab1", out var tab) && tab.Images.Count > 1)
        {
            _tabNormal = Assets.Frame(tab.Images[0].EntryName, tab.Images[0].Frame);
            _tabSelected = Assets.Frame(tab.Images[1].EntryName, tab.Images[1].Frame);
        }
    }

    private void ReadDetailRect(int index, string controlName) =>
        _ = GuiLayoutGeometry.TryRect(Layout, controlName, out _descRects[index]);

    private int CategoryOf(ItemInfoRecord? info)
    {
        var label = info is { Label.Length: > 0 } record
            ? Ascii(record.Label)
            : Assets.Messages.Get(OtherCategorySlot);

        for (var i = 0; i < _categories.Count; i++)
        {
            if (CategoryName(i).SequenceEqual(label))
            {
                return i;
            }
        }

        _categories.Add(Store(_categoryText, ref _categoryBytes, label));
        return _categories.Count - 1;
    }

    private int RowIndex(int visibleIndex)
    {
        var index = ((_page - 1) * RowsPerPage) + visibleIndex;
        return (uint)visibleIndex >= RowsPerPage || (uint)index >= (uint)_visible.Count ? -1 : _visible[index];
    }

    private int CategoryInSlot(int slot) =>
        _tabOrigin + slot - Math.Max(0, VisibleTabs - _categories.Count);

    private int TabAt(int x, int y)
    {
        for (var i = 0; i < VisibleTabs; i++)
        {
            var index = CategoryInSlot(i);
            if (index >= 0 && index < _categories.Count && _tabRects[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnRefreshDefaultActionState() => UpdateButtons();

    private void UpdateButtons()
    {
        if (_ok is not null)
        {
            _ok.Enabled = _selectedRow >= 0;
        }

        if (_tabPrev is not null)
        {
            _tabPrev.Enabled = _tabOrigin > 0;
        }

        if (_tabNext is not null)
        {
            _tabNext.Enabled = _tabOrigin + VisibleTabs < _categories.Count;
        }

        if (_pagePrev is not null)
        {
            _pagePrev.Enabled = _page > 1;
        }

        if (_pageNext is not null)
        {
            _pageNext.Enabled = _page < _pageCount;
        }
    }

    public void SelectRow(int visibleIndex)
    {
        _selectedRow = visibleIndex >= 0 && visibleIndex < VisibleRowCount ? visibleIndex : -1;
        UpdateButtons();
        Invalidate();
    }

    public void Hover(int visibleIndex, int paneX, int paneY)
    {
        if (visibleIndex < 0)
        {
            if (_hoverRow >= 0)
            {
                _hoverRow = -1;
                TooltipChanged?.Invoke(default, 0, 0);
            }

            return;
        }

        if (RowIndex(visibleIndex) != _detailRow)
        {
            ShowDetail(visibleIndex);
        }

        _hoverRow = visibleIndex;

        TooltipChanged?.Invoke(
            Tooltip(visibleIndex),
            Bounds.X + paneX + TooltipOffsetX,
            Bounds.Y + paneY + TooltipOffsetY);
    }

    private void DrawTabs(Canvas canvas)
    {
        for (var i = 0; i < VisibleTabs; i++)
        {
            var index = CategoryInSlot(i);
            if (index < 0 || index >= _categories.Count)
            {
                continue;
            }

            var rect = _tabRects[i];

            var art = index == _category ? _tabSelected : _tabNormal;
            if (!art.IsEmpty)
            {
                canvas.BlitPixmap(rect.X, rect.Y, in art, BlendMode.Transparent);
            }

            var label = CategoryName(index);
            if (label.IsEmpty)
            {
                continue;
            }

            var labelBox = new PixelRect(
                rect.X + _tabLabelRect.X, rect.Y + _tabLabelRect.Y, _tabLabelRect.Width, _tabLabelRect.Height);
            var offset = (_tabLabelRect.Width - (label.Length * GlyphWidth)) / 2;

            _wrapped.MaxWidth = Math.Max(1, labelBox.Width);
            _wrapped.MaxLines = 1;

            _wrapped.BreaksOnWordBoundary = false;
            _wrapped.SetText(label);

            var previousClip = canvas.Clip;
            canvas.SetClip(rect);
            _wrapped.Draw(canvas, labelBox.X + offset, labelBox.Y);
            canvas.SetClip(previousClip);
        }
    }

    private void DrawRows(Canvas canvas)
    {
        for (var i = 0; i < RowsPerPage; i++)
        {
            if (!TryRow(i, out var row))
            {
                break;
            }

            var top = _contentRect.Y + (i * RowPitch);
            var icon = row.Icon;
            if (!icon.Image.IsEmpty)
            {
                canvas.BlitPixmap(
                    _iconRect.X + icon.Left,
                    top + (_iconRect.Y - _contentRect.Y) + icon.Top,
                    icon.Image,
                    BlendMode.Transparent);
            }

            var lineTop = top + (_nameRect.Y - _contentRect.Y);
            var valueLength = FormatNumber(row.Value);
            var valueLeft = _nameRect.Right - (valueLength * GlyphWidth);

            _text.DefaultPaletteIndex = i == _selectedRow ? SelectedTextColor : TextColor;
            _text.MaxWidth = Math.Max(1, valueLength * GlyphWidth);
            _text.SetText(_digits.AsSpan(0, valueLength));
            _text.Draw(canvas, valueLeft, lineTop);

            var nameRight = valueLeft;
            if (row.HasQuantity && row.Quantity == 0)
            {
                nameRight = valueLeft - (SoldOutMarker.Length * GlyphWidth);
                _text.DefaultPaletteIndex = SelectedTextColor;
                _text.MaxWidth = SoldOutMarker.Length * GlyphWidth;
                _text.SetText(SoldOutMarker);
                _text.Draw(canvas, nameRight, lineTop);
                _text.DefaultPaletteIndex = i == _selectedRow ? SelectedTextColor : TextColor;
            }

            var name = _names.AsSpan(row.Name.Start, row.Name.Length);
            var room = Math.Max(GlyphWidth, nameRight - _nameRect.X);
            _text.MaxWidth = room;
            _text.SetText(name[..Math.Min(name.Length, room / GlyphWidth)]);
            _text.Draw(canvas, _nameRect.X, lineTop);
        }

        _text.DefaultPaletteIndex = TextColor;
    }

    private void DrawDetail(Canvas canvas)
    {
        for (var i = 0; i < _descRects.Length; i++)
        {
            var line = DetailLine(i);
            if (line.IsEmpty)
            {
                continue;
            }

            var rect = _descRects[i];
            var block = i == DescriptionLine ? _wrapped : _text;
            block.MaxWidth = Math.Max(1, rect.Width);
            block.MaxLines = Math.Max(1, line.Length);

            block.BreaksOnWordBoundary = i == DescriptionLine;
            block.SetText(line);

            canvas.SetClip(in rect);
            block.Draw(canvas, rect.X, rect.Y);
            canvas.ResetClip();
        }
    }

    private void DrawPage(Canvas canvas)
    {
        Span<byte> text = stackalloc byte[16];
        var length = Digits(_page, text);
        text[length++] = (byte)' ';
        text[length++] = (byte)'/';
        text[length++] = (byte)' ';
        length += Digits(_pageCount, text[length..]);

        var center = (_pageRect.X + _pageRect.Right) / 2;
        _text.MaxWidth = Math.Max(1, _pageRect.Width);
        _text.SetText(text[..length]);
        _text.Draw(canvas, center - (length * GlyphWidth / 2), _pageRect.Bottom - LineHeight);
    }
}
