using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record SpellSkillEntry(
    string Name,
    int Level,
    int SecondLevel,
    int ThirdLevel,
    int Frame,
    int X,
    int Y,
    int Group)
{
    public int Tab => ThirdLevel != 0 ? 2 : SecondLevel != 0 ? 1 : 0;
}

public sealed class SpellSkillViewPane : LayoutPane
{
    protected override bool ConsumesKeyboardEvents => false;

    public const string LayoutName = "llspell.txt";

    public const string FrameControl = "Noname";

    public const string ContentControl = "InContent";

    public const string PropertyControl = "Property";

    public static readonly string[] ButtonControls = ["BTN1", "BTN2", "BTN3"];

    public const int CellSize = 0x20;

    public const int BackgroundModes = 3;

    public const int PropertyCentreX = 0x140;

    public const int PropertyCentreY = 0xF0;

    public const int HintDrop = 0x10;

    private readonly bool _isSpell;

    private readonly string[] _sheets;

    private readonly PixelRect _contentRect;
    private readonly PixelRect _propertyRect;
    private readonly PixelRect[] _buttonRects = new PixelRect[3];
    private readonly Pixmap[] _buttonArt = new Pixmap[6];
    private readonly Pixmap _frame;
    private readonly Pixmap[] _backgrounds = new Pixmap[BackgroundModes];

    private readonly List<SpellSkillEntry> _rows = [];

    private Pixmap _classSheet;
    private int _background;
    private int _subPage;
    private bool _buttonsOn;
    private int _buttonCount;

    public SpellSkillViewPane(UiAssets assets, PixelBlender blender, bool isSpell, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        _isSpell = isSpell;
        _sheets =
        [
            isSpell ? "spell001.epf" : "skill001.epf",
            isSpell ? "spell002.epf" : "skill002.epf",
            isSpell ? "spell003.epf" : "skill003.epf",
        ];

        GuiLayoutGeometry.TryRect(Layout, ContentControl, out _contentRect);
        GuiLayoutGeometry.TryRect(Layout, PropertyControl, out _propertyRect);

        _frame = ControlFrame(FrameControl, 0);

        for (var i = 0; i < BackgroundModes; i++)
        {
            _backgrounds[i] = ControlFrame(ContentControl, i);
        }

        for (var i = 0; i < ButtonControls.Length; i++)
        {
            GuiLayoutGeometry.TryRect(Layout, ButtonControls[i], out _buttonRects[i]);
            _buttonArt[i * 2] = ControlFrame(ButtonControls[i], 0);
            _buttonArt[(i * 2) + 1] = ControlFrame(ButtonControls[i], 1);
        }
    }

    public bool IsSpell => _isSpell;

    public IReadOnlyList<SpellSkillEntry> Rows => _rows;

    public int BackgroundMode
    {
        get => _background;
        set
        {
            if (_background == value)
            {
                return;
            }

            _background = value;
            Invalidate();
        }
    }

    public static PixelRect Placement(UiAssets assets) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, FrameControl);

    public void SetRows(IEnumerable<SpellSkillEntry> rows, bool buttonsOn, int buttonCount)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _buttonsOn = buttonsOn;
        if (buttonsOn)
        {
            _buttonCount = buttonCount;
        }

        _subPage = 0;
        _rows.Clear();
        _rows.AddRange(rows);
        Invalidate();
    }

    public void SetClassSheet(UiAssets assets, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _classSheet = string.IsNullOrEmpty(entryName) ? default : assets.LayoutFrame(entryName, 0);
        Invalidate();
    }

    public PixelRect CellOf(SpellSkillEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new PixelRect(entry.X + _contentRect.X, entry.Y + _contentRect.Y, CellSize, CellSize);
    }

    public bool Shows(SpellSkillEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return !_buttonsOn || entry.Group == _subPage;
    }

    public string SheetFor(bool owned, bool available) => _sheets[owned ? 0 : available ? 1 : 2];

    public PixelRect PropertyPlacement() =>
        new(PropertyCentreX - (_propertyRect.Width / 2),
            PropertyCentreY - (_propertyRect.Height / 2),
            _propertyRect.Width,
            _propertyRect.Height);

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (!_frame.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _frame, BlendMode.Transparent);
        }

        if (_background > 0 && _background <= BackgroundModes)
        {
            var art = _backgrounds[_background - 1];
            if (!art.IsEmpty)
            {
                canvas.BlitPixmap(_contentRect.X, _contentRect.Y, in art, BlendMode.Transparent);
            }
        }

        if (!_classSheet.IsEmpty)
        {
            canvas.BlitPixmap(_contentRect.X, _contentRect.Y, in _classSheet, BlendMode.Transparent);
        }

        foreach (var row in _rows)
        {
            if (!Shows(row))
            {
                continue;
            }

            var cell = CellOf(row);
            var icon = Assets.LayoutFrame(SheetFor(owned: false, available: true), row.Frame);
            if (!icon.IsEmpty)
            {
                canvas.BlitPixmap(cell.X, cell.Y, in icon, BlendMode.Transparent);
            }
        }

        if (!_buttonsOn)
        {
            return;
        }

        for (var i = 0; i < _buttonCount && i < ButtonControls.Length; i++)
        {
            var art = _buttonArt[(i * 2) + (i == _subPage ? 1 : 0)];
            if (!art.IsEmpty)
            {
                canvas.BlitPixmap(_buttonRects[i].X, _buttonRects[i].Y, in art, BlendMode.Transparent);
            }
        }
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type == UiEventType.LeftDown && _buttonsOn)
        {
            for (var i = 0; i < _buttonCount && i < ButtonControls.Length; i++)
            {
                if (_buttonRects[i].Contains(pointer.X, pointer.Y))
                {
                    _subPage = i;
                    Invalidate();
                    return true;
                }
            }
        }

        if (pointer.Type != UiEventType.LeftUp)
        {
            return base.OnPointer(in pointer);
        }

        foreach (var row in _rows)
        {
            if (Shows(row) && CellOf(row).Contains(pointer.X, pointer.Y))
            {
                return true;
            }
        }

        return base.OnPointer(in pointer);
    }
}

public sealed class SpellSkillInfoPane : SessionLayeredTabPane
{
    public const string LayoutName = "llinfo.txt";

    public const string TabStripControl = "BTab";

    public const string ContentControl = "Content";

    public const int PageCount = 3;

    public const int FirstTabTemplate = 4;

    public const int TabImageIndex = 1;

    public const string SpellPrefix = "lssp";

    public const string SkillPrefix = "lssk";

    public const string SharedThirdTabEntry = "lsspsk2.epf";

    public static readonly string[] ClassLetters = ["2", "w", "r", "z", "p", "m"];

    public static readonly string[] TabSuffixes = ["", "m", "2"];

    public const int MonkClass = 5;

    public const int WarriorClass = 1;

    public const int MonkSubPages = 3;

    private readonly bool _isSpell;
    private readonly string _prefix;
    private readonly SpellSkillViewPane _view;
    private readonly List<SpellSkillEntry>[] _tabs = [[], [], []];

    private int _playerClass;
    private bool _tableReady;

    public SpellSkillInfoPane(UiAssets assets, PixelBlender blender, bool isSpell)
        : base(assets, blender, LayoutName, Placement(assets, TabStripControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        _isSpell = isSpell;
        _prefix = isSpell ? SpellPrefix : SkillPrefix;
        var column = Placement(assets, TabStripControl);

        for (var i = 0; i < PageCount; i++)
        {
            var tab = $"BTab{FirstTabTemplate + i}";
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

        _view = new SpellSkillViewPane(assets, blender, isSpell, Placement(assets, ContentControl))
        {
            Visible = false,
        };
    }

    public bool IsSpell => _isSpell;

    public string Prefix => _prefix;

    public SpellSkillViewPane View => _view;

    public int PlayerClass
    {
        get => _playerClass;
        set => _playerClass = value;
    }

    public static PixelRect Placement(UiAssets assets, string control) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, control);

    public string EntryNameFor(int playerClass, int tab)
    {
        if (playerClass is < 1 or > 5)
        {
            return string.Empty;
        }

        if (tab == 2)
        {
            return SharedThirdTabEntry;
        }

        var suffix = (uint)tab < (uint)TabSuffixes.Length ? TabSuffixes[tab] : string.Empty;
        return $"{_prefix}{ClassLetters[playerClass]}{suffix}.epf";
    }

    public void Add(SpellSkillEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        _tabs[entry.Tab].Add(entry);
    }

    public void Clear()
    {
        foreach (var tab in _tabs)
        {
            tab.Clear();
        }

        _tableReady = false;
    }

    public void Finish(bool ready)
    {
        _tableReady = ready;
        if (ready)
        {
            Feed(CurrentPage < 0 ? 0 : CurrentPage);
        }
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

    public void Feed(int tab)
    {
        if ((uint)tab >= (uint)_tabs.Length)
        {
            return;
        }

        var monkThirdTab = tab == 2 && _playerClass == MonkClass;
        _view.SetRows(_tabs[tab], monkThirdTab, monkThirdTab ? MonkSubPages : 1);
    }

    public int BackgroundFor(int tab)
    {
        if (tab != 0)
        {
            return 0;
        }

        return _playerClass == WarriorClass && !_isSpell ? 2 : 1;
    }

    protected override void OnPageEnter(int index)
    {
        _view.SetClassSheet(Assets, EntryNameFor(_playerClass, index));
        _view.BackgroundMode = BackgroundFor(index);

        if (_tableReady)
        {
            Feed(index);
        }
    }

    protected override void OnPageLeave(int index)
    {
    }
}
