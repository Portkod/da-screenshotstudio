using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class AlphaScreenPane : Pane
{
    public const byte ColorIndex = 0x1F;

    public const string ControlName = "UIScreen";

    private readonly ushort _color;

    public AlphaScreenPane(in PixelRect bounds, PixelBlender blender, ushort color)
        : base(in bounds, blender)
    {
        _color = color;
        Visible = false;
    }

    public static int WeightFor(int row, int height) =>
        height <= 0 ? 0 : row * PixelBlender.MaxAlphaWeight / height;

    protected override void OnComposite(Canvas target, int absoluteX, int absoluteY)
    {
        var height = Bounds.Height;
        var width = Bounds.Width;

        for (var row = 0; row < height; row++)
        {
            target.BlendRect(
                new PixelRect(absoluteX, absoluteY + row, width, 1),
                _color,
                WeightFor(row, height));
        }
    }
}
