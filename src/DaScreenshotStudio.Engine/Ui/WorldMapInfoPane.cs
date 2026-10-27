using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed record WorldMapNode(string Key, string Name, int X, int Y, int Third, string Fourth)
{
    public bool IsPlaced => X != 0 || Y != 0;
}

public sealed class WorldMapTables
{
    public const int Capacity = 0x100;

    public const int RequiredValues = 5;

    private readonly List<WorldMapNode>?[] _halves = new List<WorldMapNode>?[2];

    public IReadOnlyList<WorldMapNode> Nodes(int half) =>
        (uint)half < 2 ? _halves[half] ?? [] : [];

    public bool Load(int half, IReadOnlyList<WorldMapNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        if ((uint)half >= 2 || nodes.Count == 0)
        {
            return false;
        }

        if (_halves[half] is { Count: > 0 })
        {
            return true;
        }

        var kept = new List<WorldMapNode>(Math.Min(nodes.Count, Capacity));
        foreach (var node in nodes)
        {
            if (kept.Count >= Capacity)
            {
                break;
            }

            kept.Add(node);
        }

        _halves[half] = kept;
        return true;
    }
}

public sealed class WorldMapViewPane : LayoutPane
{
    protected override bool ConsumesKeyboardEvents => false;

    public const string LayoutName = "llmap.txt";

    public const string BackgroundControl = "Noname";

    public const string MapControl = "Map";

    public const int East = 0;

    public const int West = 1;

    public const string FieldName = "field001";

    private readonly PixelRect _mapRect;
    private readonly Pixmap _backdrop;
    private readonly Pixmap[] _maps = new Pixmap[2];

    private int _half;
    private IReadOnlyList<WorldMapNode> _nodes = [];

    public WorldMapViewPane(UiAssets assets, PixelBlender blender, PixelRect? bounds = null)
        : base(assets, blender, LayoutName, bounds ?? Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        GuiLayoutGeometry.TryRect(Layout, MapControl, out _mapRect);
        _backdrop = ControlFrame(BackgroundControl, 0);
        _maps[East] = ControlFrame(MapControl, East);
        _maps[West] = ControlFrame(MapControl, West);
    }

    public int Half
    {
        get => _half;
        set
        {
            var clamped = Math.Clamp(value, East, West);
            if (_half == clamped)
            {
                return;
            }

            _half = clamped;
            Invalidate();
        }
    }

    public IReadOnlyList<WorldMapNode> Nodes => _nodes;

    public static PixelRect Placement(UiAssets assets) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, BackgroundControl);

    public void Rebuild(WorldMapTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var kept = new List<WorldMapNode>();
        foreach (var node in tables.Nodes(_half))
        {
            if (node.IsPlaced)
            {
                kept.Add(node);
            }
        }

        _nodes = kept;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        if (!_backdrop.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _backdrop, BlendMode.Transparent);
        }

        var map = _maps[_half];
        if (!map.IsEmpty)
        {
            canvas.BlitPixmap(_mapRect.X, _mapRect.Y, in map, BlendMode.Transparent);
        }
    }
}

public sealed class WorldMapInfoPane : SessionLayeredTabPane
{
    public const string LayoutName = "llinfo.txt";

    public const string TabStripControl = "BTab";

    public const int PageCount = 2;

    public const string TabTemplate = "WTab";

    public const int FirstMessageId = 0x00100008;

    private readonly WorldMapViewPane _view;
    private readonly WorldMapTables _tables = new();

    public WorldMapInfoPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Placement(assets, TabStripControl))
    {
        ArgumentNullException.ThrowIfNull(assets);

        var column = Placement(assets, TabStripControl);

        for (var i = 0; i < PageCount; i++)
        {
            var tab = $"{TabTemplate}{i + 1}";
            GuiLayoutGeometry.TryRect(Layout, tab, out var tabRect);
            GuiLayoutGeometry.TryRect(Layout, $"{tab}R", out var stripRect);

            var art = ControlFrame(tab, 0);
            AddTab(
                new PixelRect(tabRect.X - column.X, tabRect.Y - column.Y, tabRect.Width, tabRect.Height),
                art,
                art,
                default,
                in stripRect,
                ControlFrame($"{tab}R", 0));
        }

        _view = new WorldMapViewPane(assets, blender, Placement(assets, LegendInfoPane.ContentControl))
        {
            Visible = false,
        };
    }

    public WorldMapViewPane View => _view;

    public WorldMapTables Tables => _tables;

    public static PixelRect Placement(UiAssets assets, string control) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, control);

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

    protected override void OnPageEnter(int index)
    {
        _view.Half = index == 1 ? WorldMapViewPane.West : WorldMapViewPane.East;
        _view.Rebuild(_tables);
    }

    protected override void OnPageLeave(int index)
    {
    }
}
