using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

internal sealed class DialogSkin
{
    public const string BackgroundArtName = "DlgBack2.spf";

    public const string FrameArtName = "DlgFrame.epf";

    public const int FrameTileSize = 0x10;

    public const int FrameTileCount = 8;

    public const byte NamedBackgroundFillIndex = 0x18;

    private readonly Pixmap _background;
    private readonly Pixmap[] _frames = new Pixmap[FrameTileCount];

    public DialogSkin(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _background = assets.Frame(BackgroundArtName, 0);
        for (var i = 0; i < _frames.Length; i++)
        {
            _frames[i] = assets.PaletteFrame(FrameArtName, i);
        }
    }

    public void Draw(Canvas canvas)
    {
        DrawBackground(canvas);
        DrawBorder(canvas);
    }

    public void DrawBackground(Canvas canvas) => Tile(canvas, in _background);

    public static void DrawNamedBackground(Canvas canvas, in Pixmap art, ushort fill)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        if (art.IsEmpty)
        {
            return;
        }

        canvas.FillRect(new PixelRect(0, 0, canvas.Width, canvas.Height), fill, BlendMode.Copy);
        Tile(canvas, in art);
    }

    public void DrawBorder(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);

        var width = canvas.Width;
        var height = canvas.Height;
        var last = height - FrameTileSize;
        var right = width - FrameTileSize;

        for (var x = FrameTileSize; x + FrameTileSize < width - 1; x += FrameTileSize)
        {
            Piece(canvas, 1, x, 0);
            Piece(canvas, 6, x, last);
        }

        for (var y = FrameTileSize; y + FrameTileSize < height - 1; y += FrameTileSize)
        {
            Piece(canvas, 3, 0, y);
            Piece(canvas, 4, right, y);
        }

        Piece(canvas, 0, 0, 0);
        Piece(canvas, 2, right, 0);
        Piece(canvas, 5, 0, last);
        Piece(canvas, 7, right, last);
    }

    private static void Tile(Canvas canvas, in Pixmap art)
    {
        if (art.IsEmpty)
        {
            return;
        }

        for (var y = 0; y < canvas.Height; y += art.Height)
        {
            for (var x = 0; x < canvas.Width; x += art.Width)
            {
                canvas.BlitPixmap(x, y, in art, BlendMode.Copy);
            }
        }
    }

    private void Piece(Canvas canvas, int frame, int x, int y)
    {
        if (!_frames[frame].IsEmpty)
        {
            canvas.BlitPixmap(x, y, in _frames[frame], BlendMode.Transparent);
        }
    }
}
