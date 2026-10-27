using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LogViewPane : GuiChildPane
{
    private readonly LogComposer _composer;

    public LogViewPane(
        in PixelRect bounds,
        PixelBlender blender,
        GameFont font,
        TextPalette palette,
        TextLog log,
        int contentWidth)
        : base(in bounds, blender)
    {
        ContentWidth = Math.Max(1, contentWidth);
        _composer = new LogComposer(font, palette, log, ContentWidth, bounds.Height);
    }

    public int ContentWidth { get; }

    public TextLog Log => _composer.Log;

    public LogComposer Composer => _composer;

    public int Rows => _composer.Rows;

    public int Maximum => _composer.Maximum;

    public int Scroll
    {
        get => _composer.Position;
        set
        {
            if (_composer.Position == value)
            {
                return;
            }

            _composer.Position = value;
            Invalidate();
        }
    }

    public byte DefaultPaletteIndex
    {
        get => _composer.DefaultPaletteIndex;
        set => _composer.DefaultPaletteIndex = value;
    }

    public void Refresh()
    {
        if (_composer.Refresh())
        {
            Invalidate();
        }
    }

    public void ScrollBy(int rows) => Scroll = _composer.Position + rows;

    public void ScrollToEnd()
    {
        if (_composer.Position == _composer.Maximum)
        {
            return;
        }

        _composer.ScrollToEnd();
        Invalidate();
    }

    public bool ScrollsOnWheel { get; set; } = true;

    protected override void ScrollByNotches(int notches)
    {
        if (ScrollsOnWheel)
        {
            ScrollBy(-notches);
        }
    }

    protected override void OnBoundsChanged() => _composer.Resize(ContentWidth, Bounds.Height);

    protected override void OnDrawContent(Canvas canvas)
    {
        PaintBackground(canvas);
        _composer.Draw(canvas, 0, 0);
    }
}
