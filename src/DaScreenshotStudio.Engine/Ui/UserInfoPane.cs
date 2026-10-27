using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public static class UserInfoShell
{
    public const string LayoutName = "_nui.txt";

    public const string ContentControl = "CONTENT";

    public static PixelRect Content(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return GuiLayoutGeometry.TryRect(assets.Layout(LayoutName), ContentControl, out var rect)
            ? rect
            : PixelRect.Empty;
    }

    public static PixelRect Bounds(UiAssets assets)
    {
        var content = Content(assets);
        var strip = UserInfoTabPane.StripBounds(assets);

        if (content.Width == 0 || content.Height == 0)
        {
            return strip;
        }

        if (strip.Width == 0 || strip.Height == 0)
        {
            return content;
        }

        return PixelRect.FromEdges(
            Math.Min(content.X, strip.X),
            Math.Min(content.Y, strip.Y),
            Math.Max(content.Right, strip.Right),
            Math.Max(content.Bottom, strip.Bottom));
    }
}

public sealed class UserInfoTabPane : Pane
{
    public const int TabCount = 7;

    public const int CloseTab = 6;

    public const int NoTab = -1;

    private const string InactiveControl = "TAB_INTRO_E";

    private static readonly string[] TabControls =
    [
        "TAB_INTRO", "TAB_LEGEND", "TAB_SKILL", "TAB_EVENT", "TAB_ALBUM", "TAB_FAMILY", "TAB_CLOSE",
    ];

    private readonly Pixmap[] _current = new Pixmap[TabCount];
    private readonly Pixmap[] _inactive = new Pixmap[TabCount];
    private readonly PixelRect[] _currentRects = new PixelRect[TabCount];
    private readonly PixelRect[] _inactiveRects = new PixelRect[TabCount];
    private readonly bool[] _enabled = new bool[TabCount];

    private int _tab = NoTab;

    public UserInfoTabPane(UiAssets assets, PixelBlender blender, bool forUser)
        : base(Place(assets), blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        TargetBlend = BlendMode.Transparent;
        Visible = false;

        EnabledTabs(forUser, _enabled);

        var layout = assets.Layout(UserInfoShell.LayoutName);
        if (layout is null || !layout.TryFind(TabControls[0], out var first)
            || !layout.TryFind(InactiveControl, out var inactive))
        {
            return;
        }

        var origin = Bounds;
        var anchor = GuiLayoutGeometry.Rect(first);
        var stub = GuiLayoutGeometry.Rect(inactive);
        var stubEntry = inactive.Images.Count > 0 ? inactive.Images[0].EntryName : null;

        for (var i = 0; i < TabCount; i++)
        {
            if (!layout.TryFind(TabControls[i], out var control))
            {
                continue;
            }

            var rect = GuiLayoutGeometry.Rect(control);
            _currentRects[i] = rect.Translate(-origin.X, -origin.Y);
            _current[i] = assets.Frame(control, 0);

            if (i == CloseTab)
            {
                _inactiveRects[i] = _currentRects[i];
                _inactive[i] = _current[i];
                continue;
            }

            _inactiveRects[i] = stub
                .Translate(rect.X - anchor.X, rect.Y - anchor.Y)
                .Translate(-origin.X, -origin.Y);

            if (stubEntry is not null && control.Images.Count > 0)
            {
                _inactive[i] = assets.Frame(stubEntry, control.Images[0].Frame);
            }
        }
    }

    public int CurrentTab
    {
        get => _tab;
        set
        {
            if (_tab == value)
            {
                return;
            }

            _tab = value;
            Invalidate();
        }
    }

    public bool IsEnabled(int tab) => (uint)tab < (uint)TabCount && _enabled[tab];

    public static void EnabledTabs(bool forUser, Span<bool> enabled)
    {
        enabled.Clear();

        if (enabled.Length < TabCount)
        {
            return;
        }

        enabled[0] = true;
        enabled[1] = true;
        enabled[CloseTab] = true;

        if (!forUser)
        {
            return;
        }

        for (var i = 0; i < TabCount; i++)
        {
            enabled[i] = true;
        }
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        if (key.Key is not (UiKeys.Escape or UiKeys.Space))
        {
            return false;
        }

        return true;
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible || pointer.Type != UiEventType.LeftDown)
        {
            return false;
        }

        for (var i = 0; i < TabCount; i++)
        {
            if (i == _tab || !_enabled[i] || !_inactiveRects[i].Contains(pointer.X, pointer.Y))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!Visible)
        {
            return;
        }

        for (var i = 0; i < TabCount; i++)
        {
            if (!_enabled[i])
            {
                continue;
            }

            var art = i == _tab ? _current[i] : _inactive[i];
            if (art.IsEmpty)
            {
                continue;
            }

            var rect = i == _tab ? _currentRects[i] : _inactiveRects[i];

            canvas.BlitPixmap(rect.X, rect.Y, in art, BlendMode.Transparent);
        }
    }

    public static PixelRect StripBounds(UiAssets assets) => Place(assets);

    private static PixelRect Place(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var layout = assets.Layout(UserInfoShell.LayoutName);
        if (layout is null || !layout.TryFind(TabControls[0], out var first)
            || !layout.TryFind(InactiveControl, out var inactive))
        {
            return PixelRect.Empty;
        }

        var anchor = GuiLayoutGeometry.Rect(first);
        var stub = GuiLayoutGeometry.Rect(inactive);
        var union = anchor;

        for (var i = 0; i < TabCount; i++)
        {
            if (!layout.TryFind(TabControls[i], out var control))
            {
                continue;
            }

            var rect = GuiLayoutGeometry.Rect(control);
            union = Union(union, rect);
            union = Union(
                union,
                i == CloseTab ? rect : stub.Translate(rect.X - anchor.X, rect.Y - anchor.Y));
        }

        return union;
    }

    private static PixelRect Union(in PixelRect a, in PixelRect b)
    {
        if (a.IsEmpty)
        {
            return b;
        }

        if (b.IsEmpty)
        {
            return a;
        }

        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        return PixelRect.FromEdges(
            left,
            top,
            Math.Max(a.X + a.Width, b.X + b.Width),
            Math.Max(a.Y + a.Height, b.Y + b.Height));
    }
}
