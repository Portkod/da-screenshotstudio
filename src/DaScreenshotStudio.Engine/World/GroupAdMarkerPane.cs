using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Ui;

namespace DaScreenshotStudio.Engine.World;

public sealed class GroupAdMarkerPane : Pane, IGroupAdMarkerArt
{
    public const string LayoutName = "lgcpane.txt";

    public const string PlateControl = "Noname";

    public const string TitleControl = "TITLE";

    public const int CellWidth = ObjectNamePane.CellWidth;

    public const int Lift = 74;

    public const byte TextPalette = Text.TextPalette.InterfaceIndex;

    public const byte InteriorIndex = 0xFF;

    private readonly WorldScene _scene;
    private readonly TextBlock _text;
    private readonly Pixmap _plate;
    private readonly PixelRect _title;
    private int _centerBiasY;

    public GroupAdMarkerPane(in PixelRect bounds, PixelBlender blender, WorldScene scene, UiAssets assets)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(assets);

        _scene = scene;

        var layout = assets.Layout(LayoutName);
        if (layout is { ControlCount: > 0 } && layout[0].Images.Count > 0)
        {
            var image = layout[0].Images[0];
            _plate = assets.LayoutFrame(image.EntryName, image.Frame);
        }

        _title = layout is not null && GuiLayoutGeometry.TryRect(layout, TitleControl, out var title)
            ? title
            : PixelRect.Empty;

        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(_title.Width, 1), 1)
        {
            Wraps = false,
            DefaultPaletteIndex = TextPalette,
        };
    }

    public PixelRect Plate => new(0, 0, _plate.Width, _plate.Height);

    public bool Enabled { get; set; } = true;

    public void ApplyViewLayout(in WorldViewLayout layout)
    {
        _centerBiasY = layout.CenterBiasY;
        SetBounds(layout.PaneBounds);
    }

    protected override bool DrawsNothing => true;

    void IGroupAdMarkerArt.DrawMarker(Canvas canvas, in PixelRect box, ReadOnlySpan<byte> text)
    {
        canvas.BlitPixmap(box.X, box.Y, in _plate, BlendMode.Darken);

        _text.SetText(text);

        var left = box.X + ((_title.Right + _title.X - (text.Length * CellWidth)) / 2);
        _text.Draw(canvas, left, box.Y + _title.Bottom - _text.LineHeight);
    }
}

public interface IGroupAdMarkerArt
{
    bool Enabled { get; }

    PixelRect Plate { get; }

    void DrawMarker(Canvas canvas, in PixelRect box, ReadOnlySpan<byte> text);
}
