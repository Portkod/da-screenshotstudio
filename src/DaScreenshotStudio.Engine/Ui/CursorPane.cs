using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public enum CursorMode
{
    Normal = 0,

    Target = 1,

    Unused = 2,

    Blank = 3,
}

public sealed class CursorPane : Pane
{
    public const string SheetName = "mouse.epf";

    public const int HotspotX = 0xC;

    public const int HotspotY = 7;

    public const int CanvasSize = 0x20;

    private readonly Pixmap[] _frames = new Pixmap[4];
    private CursorMode _mode;
    private int _pointerX = -1;
    private int _pointerY = -1;
    private bool _needsPresent;

    private CursorPane(PixelBlender blender)
        : base(new PixelRect(0, 0, CanvasSize, CanvasSize), blender)
    {
        Visible = false;

        TargetBlend = BlendMode.Transparent;
    }

    public static CursorPane? TryLoad(UiAssets assets, PixelBlender blender)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(blender);

        var frames = assets.PaletteFrameCount(SheetName);
        if (frames <= 0)
        {
            return null;
        }

        var pane = new CursorPane(blender);
        for (var i = 0; i < pane._frames.Length; i++)
        {
            pane._frames[i] = i != (int)CursorMode.Blank && i < frames
                ? assets.PaletteFrame(SheetName, i)
                : default;
        }

        return pane;
    }

    public CursorMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            _needsPresent = true;
            Invalidate();
        }
    }

    public bool TakeNeedsPresent()
    {
        var needed = _needsPresent;
        _needsPresent = false;
        return needed;
    }

    public int PointerX => _pointerX;

    public int PointerY => _pointerY;

    public void MoveTo(int x, int y)
    {
        if (_pointerX == x && _pointerY == y)
        {
            return;
        }

        _pointerX = x;
        _pointerY = y;
        Visible = true;
        _needsPresent = true;
        SetBounds(new PixelRect(x - HotspotX, y - HotspotY, CanvasSize, CanvasSize));
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var frame = _frames[(int)_mode & 3];
        if (!frame.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in frame, BlendMode.Transparent);
        }
    }
}
