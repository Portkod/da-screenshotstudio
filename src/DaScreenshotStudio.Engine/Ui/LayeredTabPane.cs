using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public readonly record struct LayeredTab(
    int ChildIndex,
    PixelRect StripRect,
    Pixmap StripImage,
    int ShortcutKey)
{
    public const int NoShortcut = -1;

    public bool HasShortcut => ShortcutKey >= 0;
}

public abstract class LayeredTabPane : LayoutPane
{
    public const string BackgroundEntryName = "noback.epf";

    private readonly List<LayeredTab> _tabs = [];
    private readonly List<ImageButtonPane> _buttons = [];
    private readonly List<bool> _enabled = [];

    private LayeredTabImagePane? _strip;
    private int _pending = -1;
    private bool _hasPending;

    protected LayeredTabPane(UiAssets assets, PixelBlender blender, string layoutName)
        : base(assets, blender, layoutName)
    {
        ConsumesPointerInside = true;
    }

    protected LayeredTabPane(UiAssets assets, PixelBlender blender, string layoutName, in PixelRect bounds)
        : base(assets, blender, layoutName, in bounds)
    {
        ConsumesPointerInside = true;
    }

    public int TabCount => _tabs.Count;

    public LayeredTabImagePane? Strip => _strip;

    public Pane? StripParent { get; private set; }

    protected int AddTab(
        in PixelRect rect,
        Pixmap normal,
        Pixmap pressed,
        Pixmap disabled,
        in PixelRect stripRect,
        Pixmap stripImage,
        int shortcutKey = LayeredTab.NoShortcut)
    {
        var index = _tabs.Count;

        var button = AddImageButton(in rect, normal, pressed, disabled, _ => Select(index), clipsToRect: true);

        _buttons.Add(button);
        _enabled.Add(true);
        _tabs.Add(new LayeredTab(index, stripRect, stripImage, shortcutKey));
        return index;
    }

    public void SetTabEnabled(int index, bool enabled)
    {
        if (index < 0 || index >= _enabled.Count)
        {
            return;
        }

        _enabled[index] = enabled;
        _buttons[index].Enabled = enabled;
    }

    public bool IsTabEnabled(int index) => index >= 0 && index < _enabled.Count && _enabled[index];

    public void Select(int index)
    {
        if (index < 0 || index >= _tabs.Count)
        {
            return;
        }

        _pending = index;
        _hasPending = true;
    }

    public void Tick()
    {
        if (!_hasPending)
        {
            return;
        }

        var index = _pending;
        _hasPending = false;
        _pending = -1;

        if (!IsTabEnabled(index))
        {
            return;
        }

        OnTabSwitched(index);
        RebuildStrip(index);
    }

    protected virtual void OnTabSwitched(int index)
    {
    }

    protected virtual void RebuildStrip(int index)
    {
        if (StripParent is not { } host || index < 0 || index >= _tabs.Count)
        {
            return;
        }

        if (_strip is not null)
        {
            _strip.Visible = false;
            host.Remove(_strip);
            _strip = null;
        }

        var tab = _tabs[index];
        if (tab.StripImage.IsEmpty)
        {
            return;
        }

        _strip = new LayeredTabImagePane(tab.StripRect, Blender);
        _strip.SetImage(tab.StripImage);

        host.Add(_strip);
    }

    public void SetStripParent(Pane? parent)
    {
        if (ReferenceEquals(StripParent, parent))
        {
            return;
        }

        if (_strip is not null)
        {
            _strip.Visible = false;
            StripParent?.Remove(_strip);
            _strip = null;
        }

        StripParent = parent;
    }

    protected override bool OnKey(in KeyEvent key)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (_tabs[i].HasShortcut && _tabs[i].ShortcutKey == (int)key.Key)
            {
                Select(i);
                return true;
            }
        }

        return base.OnKey(in key);
    }
}

public sealed class LayeredTabImagePane : Pane
{
    private Pixmap _image;

    public LayeredTabImagePane(in PixelRect bounds, PixelBlender blender)
        : base(in bounds, blender)
    {
        TargetBlend = BlendMode.Transparent;
    }

    public Pixmap Image => _image;

    public void SetImage(in Pixmap image)
    {
        _image = image;
        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Clear();

        if (!_image.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _image, BlendMode.Transparent);
        }
    }
}

public abstract class SessionLayeredTabPane : LayeredTabPane
{
    public const int NoPage = -1;

    protected SessionLayeredTabPane(UiAssets assets, PixelBlender blender, string layoutName)
        : base(assets, blender, layoutName)
    {
    }

    protected SessionLayeredTabPane(
        UiAssets assets, PixelBlender blender, string layoutName, in PixelRect bounds)
        : base(assets, blender, layoutName, in bounds)
    {
    }

    public int CurrentPage { get; private set; } = NoPage;

    public int LastEnteredPage { get; private set; } = NoPage;

    protected void SwitchToPage(int index, bool suppressStrip)
    {
        LeaveCurrentPage();
        EnterPage(index);

        if (!suppressStrip)
        {
            RebuildStrip(index);
        }
    }

    protected void EnterPage(int index)
    {
        CurrentPage = index;
        OnPageEnter(index);
        LastEnteredPage = index;
    }

    protected void LeaveCurrentPage()
    {
        var index = CurrentPage;
        OnPageLeave(index);
        CurrentPage = NoPage;
    }

    protected virtual void OnPageEnter(int index)
    {
    }

    protected virtual void OnPageLeave(int index)
    {
    }

    protected override void OnTabSwitched(int index) => SwitchToPage(index, suppressStrip: true);
}
