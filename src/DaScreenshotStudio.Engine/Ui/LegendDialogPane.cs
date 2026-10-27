using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LegendDialogPane : LayoutPane
{
    public const string LayoutName = "llegends.txt";

    public const int CloseAction = 2;

    public const int ScreenCenterX = 0x140;

    public const int TopMargin = 5;

    public const int TitleCellWidth = 8;

    public const int MaxRecords = byte.MaxValue;

    private readonly LegendListPane? _list;
    private readonly ScrollBarPane? _bar;
    private readonly GuiTextPane? _title;
    private readonly ushort _fill;

    public LegendDialogPane(UiAssets assets, PixelBlender blender, ReadOnlySpan<byte> title)
        : base(assets, blender, LayoutName, Placement(assets))
    {
        ArgumentNullException.ThrowIfNull(assets);

        ConsumesPointerAnywhere = true;
        Visible = false;
        _fill = assets.Palette[DialogSkin.NamedBackgroundFillIndex];

        _title = AddLabel("Title", paletteIndex: TitleInk(Layout));
        _title?.SetText(title);
        CenterTitle(title);

        BuildRoll(assets, blender, out _list, out _bar);

        AddButton("Close", _ => Hide());
    }

    public LegendListPane? List => _list;

    public ScrollBarPane? Bar => _bar;

    public GuiTextPane? Title => _title;

    public Action? Closed { get; set; }

    public void SetMarks(ReadOnlySpan<LegendMark> marks)
    {
        _list?.SetMarks(marks);
        Invalidate();
    }

    public void Show()
    {
        Visible = true;
        Invalidate();
    }

    public void Hide()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (key.Key == UiKeys.Escape)
        {
            Hide();
            return true;
        }

        return base.OnKey(in key);
    }

    protected override void OnCancel() => Hide();

    public static PixelRect Placement(UiAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (!GuiLayoutGeometry.TryRect(assets.Layout(LayoutName), "Noname", out var window))
        {
            return new PixelRect(0, 0, GuiBackPane.ScreenWidth, GuiBackPane.ScreenHeight);
        }

        return new PixelRect(ScreenCenterX - (window.Width / 2), TopMargin, window.Width, window.Height);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var art = Background;
        DialogSkin.DrawNamedBackground(canvas, in art, _fill);
    }

    private void CenterTitle(ReadOnlySpan<byte> title)
    {
        if (_title is null || !GuiLayoutGeometry.TryRect(Layout, "Title", out var box))
        {
            return;
        }

        var center = (box.Right + box.X) / 2;
        var half = title.Length * TitleCellWidth / 2;
        _title.SetBounds(new PixelRect(center - half, box.Y, Math.Max(1, box.Right - (center - half)), box.Height));
    }

    private void BuildRoll(UiAssets assets, PixelBlender blender, out LegendListPane? list, out ScrollBarPane? bar) =>
        TryBuildScrolledControl(
            "LegendList", content => new LegendListPane(in content, blender, assets), out list, out bar);

    private static byte TitleInk(Assets.Gui.GuiLayout? layout) =>
        layout is not null && layout.TryFind("Title", out var control) && control.Colors.Count > 0
            ? (byte)control.Colors[0]
            : byte.MaxValue;
}
