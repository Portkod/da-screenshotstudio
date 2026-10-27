using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class NpcNestedDialogPane : LayoutPane
{
    public const string BackgroundArtName = "DlgBack2.spf";

    public static readonly string[] FrameArtNames =
    [
        "nd_f01.spf", "nd_f05.spf", "nd_f02.spf", "nd_f06.spf",
        "nd_f07.spf", "nd_f03.spf", "nd_f08.spf", "nd_f08_1.spf", "nd_f04.spf",
    ];

    private readonly Pixmap _fill;
    private readonly Pixmap[] _frame = new Pixmap[9];
    private readonly PixelRect _messageBar;

    protected NpcNestedDialogPane(UiAssets assets, PixelBlender blender, string layoutName)
        : base(assets, blender, layoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        Visible = false;

        ConsumesPointerInside = true;

        _messageBar = NpcDialogPane.MessageBarRect(assets);

        _fill = assets.Frame(BackgroundArtName, 0);
        for (var i = 0; i < _frame.Length; i++)
        {
            _frame[i] = assets.Frame(FrameArtNames[i], 0);
        }
    }

    public int AnchorRight => _messageBar.Right;

    public int AnchorBottom => _messageBar.Y;

    protected virtual PixelRect ButtonBounds => PixelRect.Empty;

    protected void PlaceAtAnchor(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        SetBounds(new PixelRect(AnchorRight - width, AnchorBottom - height, width, height));
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (key.Key == UiKeys.Escape)
        {
            return false;
        }

        return base.OnKey(in key);
    }

    protected void DrawChrome(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        DrawFill(canvas);
        DrawFrame(canvas);
    }

    private void DrawFill(Canvas canvas)
    {
        if (_fill.IsEmpty)
        {
            return;
        }

        for (var y = 0; y < canvas.Height; y += _fill.Height)
        {
            for (var x = 0; x < canvas.Width; x += _fill.Width)
            {
                canvas.BlitPixmap(x, y, in _fill, BlendMode.Copy);
            }
        }
    }

    private void DrawFrame(Canvas canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;

        var corner = _frame[0];
        if (corner.IsEmpty)
        {
            return;
        }

        var topRight = _frame[2];
        var bottomLeft = _frame[5];
        var bottomRight = _frame[8];
        var button = ButtonBounds;

        for (var x = corner.Width; x < width - topRight.Width; x += _frame[1].Width)
        {
            Blit(canvas, 1, x, 0);
        }

        for (var y = corner.Height; y < height - bottomLeft.Height; y += _frame[3].Height)
        {
            Blit(canvas, 3, 0, y);
        }

        for (var y = topRight.Height; y < height - bottomRight.Height; y += _frame[4].Height)
        {
            Blit(canvas, 4, width - _frame[4].Width, y);
        }

        for (var x = bottomLeft.Width; x < width - bottomRight.Width; x += _frame[6].Width)
        {
            var clearOfButton = x + _frame[6].Width <= button.X || x >= button.Right;
            Blit(canvas, clearOfButton ? 7 : 6, x, height - _frame[6].Height);
        }

        Blit(canvas, 0, 0, 0);
        Blit(canvas, 2, width - topRight.Width, 0);
        Blit(canvas, 5, 0, height - bottomLeft.Height);
        Blit(canvas, 8, width - bottomRight.Width, height - bottomRight.Height);
    }

    private void Blit(Canvas canvas, int piece, int x, int y)
    {
        if (!_frame[piece].IsEmpty)
        {
            canvas.BlitPixmap(x, y, in _frame[piece], BlendMode.Transparent);
        }
    }
}
