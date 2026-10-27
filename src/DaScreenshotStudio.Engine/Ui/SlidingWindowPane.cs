using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class SlidingWindowPane : LayoutPane
{
    private WindowSlide _slide = new();
    private uint _now;

    protected SlidingWindowPane(UiAssets assets, PixelBlender blender, string layoutName, in PixelRect bounds)
        : base(assets, blender, layoutName, in bounds)
    {
    }

    public const int PanelTopOffset = 2;

    public const int PanelCenterX = GuiBackPane.ScreenWidth / 2;

    public const int PanelRight = 0x26A;

    public const int PanelInset = 0x9E;

    protected static PixelRect PanelPlacement(UiAssets assets, string layoutName, bool fromOptionPanel)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var layout = assets.Layout(layoutName);
        if (layout is not { ControlCount: > 0 })
        {
            return PixelRect.Empty;
        }

        var window = GuiLayoutGeometry.Rect(layout[0]);
        var left = fromOptionPanel
            ? PanelRight - window.Width - PanelInset
            : PanelCenterX - (window.Width / 2);

        return new PixelRect(left, PanelTopOffset, window.Width, window.Height);
    }

    protected abstract PixelRect SlideRest { get; }

    public int SlidePhase => _slide.Phase;

    public bool IsSliding => _slide.IsSliding;

    protected void BeginSlide(bool opening)
    {
        _slide.Begin(opening, _now);
        SetBounds(WindowSlide.Bounds(SlideRest, _slide.Phase));
    }

    protected void RestSlide() => _slide.Rest();

    public bool Tick(uint now)
    {
        _now = now;

        if (!_slide.Step(now))
        {
            return false;
        }

        if (_slide.IsAway)
        {
            Visible = false;
        }

        SetBounds(WindowSlide.Bounds(SlideRest, _slide.Phase));
        Invalidate();
        return true;
    }
}
