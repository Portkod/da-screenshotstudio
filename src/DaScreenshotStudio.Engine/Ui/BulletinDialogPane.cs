using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class BulletinDialogPane : LayoutPane
{
    public const string AnchorLayoutName = "lback.txt";

    public const string AnchorControlName = "BulletinRect";

    public const int SharedWidth = 0x245;

    public const int SharedHeight = 0x122;

    protected BulletinDialogPane(UiAssets assets, PixelBlender blender, string layoutName)
        : base(assets, blender, layoutName, SharedBounds(assets))
    {
        Visible = false;

        ConsumesPointerAnywhere = true;

        SetDragBounds(SharedBounds(assets));
    }

    public abstract BulletinView View { get; }

    protected byte LabelColor(scoped ReadOnlySpan<char> controlName) =>
        GuiLayoutGeometry.Color(Layout, controlName, TextPalette.DefaultIndex);

    public virtual bool ReplacedOnPush => false;

    public BulletinSession? Session { get; set; }

    protected ushort SectionId => Session?.SectionId ?? 0;

    public virtual void OnActivated() => Invalidate();

    public void GoBack() => Session?.GoBack();

    public void CloseSession() => Session?.Close();

    protected virtual bool ChainsToRailKeys => true;

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || !ChainsToRailKeys)
        {
            return base.OnKey(in key);
        }

        switch (key.Key)
        {
            case 'E' or 'Q' or 'R':
                CloseSession();
                return false;

            case 'W' or UiKeys.F7:
                CloseSession();
                return true;

            default:
                return base.OnKey(in key);
        }
    }

    protected static PixelRect SharedBounds(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (!GuiLayoutGeometry.TryRect(assets.Layout(AnchorLayoutName), AnchorControlName, out var anchor))
        {
            return new PixelRect(0, 0, SharedWidth, SharedHeight);
        }

        return new PixelRect(anchor.Right - SharedWidth, anchor.Y, SharedWidth, SharedHeight);
    }
}
