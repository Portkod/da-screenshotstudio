using DaScreenshotStudio.Engine.Rendering;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class HelpInfoPane : SessionLayeredTabPane
{
    public const string LayoutName = "llinfo.txt";

    public const string ContentControl = "Content";

    public const string TabStripControl = "BTab";

    public const int PageCount = 2;

    public const int FirstTabTemplate = 5;

    private readonly KeyboardInfoPane _keyboard;
    private readonly GuiInfoPane _gui;

    public HelpInfoPane(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName, Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        for (var i = 0; i < PageCount; i++)
        {
            var tab = $"BTab{FirstTabTemplate + i}";
            var strip = $"{tab}R";

            GuiLayoutGeometry.TryRect(Layout, tab, out var tabRect);
            GuiLayoutGeometry.TryRect(Layout, strip, out var stripRect);

            var art = ControlFrame(tab);
            AddTab(in tabRect, art, art, default, in stripRect, ControlFrame(strip));
        }

        var content = new PixelRect(0, 0, Bounds.Width, Bounds.Height);
        _keyboard = new KeyboardInfoPane(assets, blender, in content) { Visible = false };
        _gui = new GuiInfoPane(assets, blender, in content) { Visible = false };

        Add(_keyboard);
        Add(_gui);
    }

    public GuiInfoPane Gui => _gui;

    public static PixelRect Placement(UiAssets assets) =>
        GuiLayoutGeometry.ControlRect(assets, LayoutName, ContentControl);

    public void Show()
    {
        Visible = true;
        _keyboard.Visible = false;
        _gui.Visible = false;

        Select(0);
        Invalidate();
    }

    public void Hide()
    {
        LeaveCurrentPage();
        _gui.Visible = false;
        _keyboard.Visible = false;
        Visible = false;
        Invalidate();
    }

    public bool LoadGuiInfo(Assets.Metadata.Metafile? source, UiAssets assets) =>
        _gui.Load(source, assets);

    protected override void OnPageEnter(int index)
    {
        switch (index)
        {
            case 0:
                _keyboard.Visible = true;
                break;

            case 1:
                _gui.Visible = true;
                break;

            default:
                break;
        }
    }

    protected override void OnPageLeave(int index)
    {
        switch (index)
        {
            case 0:
                _keyboard.Visible = false;
                break;

            case 1:
                _gui.Visible = false;
                break;

            default:
                break;
        }
    }
}
