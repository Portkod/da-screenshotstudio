using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class AlbumTilePane : LayoutPane
{
    public override bool IsMovable => false;

    public const string LayoutName = "_nui_alb.txt";

    public const string FrameControl = "PIC";

    public const string PictureRectControl = "PICRECT";

    public const string ButtonPlateControl = "BBACK";

    public const string SaveControl = "SAVE";

    public const string RemoveControl = "REMOVE";

    private readonly IconFrame _frame;
    private readonly PixelRect _frameRect;
    private readonly IconFrame _plate;
    private readonly PixelRect _plateRect;
    private readonly PixelRect _pictureRect;
    private readonly ImageButtonPane? _save;
    private readonly ImageButtonPane? _remove;

    private Pixmap _picture;

    public AlbumTilePane(UiAssets assets, PixelBlender blender, in PixelRect bounds)
        : base(assets, blender, LayoutName, bounds)
    {
        TargetBlend = BlendMode.Transparent;

        _frame = LoadArt(assets, FrameControl);
        _plate = LoadArt(assets, ButtonPlateControl);

        GuiLayoutGeometry.TryRect(Layout, FrameControl, out _frameRect);
        GuiLayoutGeometry.TryRect(Layout, ButtonPlateControl, out _plateRect);
        GuiLayoutGeometry.TryRect(Layout, PictureRectControl, out _pictureRect);

        _save = AddButton(SaveControl, _ => { });
        _remove = AddButton(RemoveControl, _ => { });
        Unfocusable(_save);
        Unfocusable(_remove);

        SetPicture(default, -1);
    }

    public int Slot { get; private set; } = -1;

    public Pixmap Picture => _picture;

    public void SetPicture(Pixmap picture, int slot)
    {
        _picture = picture;
        Slot = picture.IsEmpty ? -1 : slot;

        var filled = !picture.IsEmpty;
        if (_save is not null)
        {
            _save.Visible = filled;
        }

        if (_remove is not null)
        {
            _remove.Visible = filled;
        }

        Invalidate();
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!_frame.Image.IsEmpty)
        {
            canvas.BlitPixmap(
                _frameRect.X + _frame.Left,
                _frameRect.Y + _frame.Top,
                in _frame.Image,
                BlendMode.Transparent);
        }

        if (_picture.IsEmpty)
        {
            return;
        }

        if (!_plate.Image.IsEmpty)
        {
            canvas.BlitPixmap(
                _plateRect.X + _plate.Left,
                _plateRect.Y + _plate.Top,
                in _plate.Image,
                BlendMode.Transparent);
        }

        canvas.BlitPixmap(_pictureRect.X, _pictureRect.Y, in _picture, BlendMode.Transparent);
    }

    private IconFrame LoadArt(UiAssets assets, string control) =>
        Layout is not null && Layout.TryFind(control, out var found) && found.Images.Count > 0
            ? assets.PlacedFrame(found.Images[0].EntryName, found.Images[0].Frame)
            : default;
}
