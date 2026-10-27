using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class BulletinBangNotifyDialog : LayoutPane
{
    public const string LayoutName = "lbang.txt";

    public const int RowHeight = 0xD;

    public const int MaxLineLength = 0x7F;

    public const byte InkColor = 0xFF;

    public const int AcceptAction = 2;

    public const int CancelAction = 3;

    private readonly TextBlock _text;
    private readonly PixelRect _list;
    private readonly List<byte[]> _lines = [];

    private int _position;

    public BulletinBangNotifyDialog(UiAssets assets, PixelBlender blender)
        : base(assets, blender, LayoutName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        ConsumesPointerAnywhere = true;
        Visible = false;

        Title = AddLabel("Title", paletteIndex: InkColor);

        GuiLayoutGeometry.TryRect(Layout, "Text", out var rect);
        _list = new PixelRect(rect.X, rect.Y, rect.Width, rect.Height / RowHeight * RowHeight);

        _text = new TextBlock(assets.Font, assets.Palette, Math.Max(1, _list.Width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
            DefaultPaletteIndex = InkColor,
        };

        AddButton("OK", _ => Answer(AcceptAction));
        AddButton("CANCEL", _ => Answer(CancelAction));
    }

    public GuiTextPane? Title { get; }

    public int VisibleRows => Math.Max(1, _list.Height / RowHeight);

    protected override bool OverOwnControl(in PointerEvent pointer) =>
        _list.Contains(pointer.X, pointer.Y);

    public int LineCount => _lines.Count;

    public void SetLines(IReadOnlyList<byte[]> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        _lines.Clear();
        _position = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            _lines.Add(Truncate(lines[i]));
        }
    }

    public ReadOnlySpan<byte> LineAt(int index) =>
        (uint)index < (uint)_lines.Count ? _lines[index] : default;

    private byte[] Truncate(byte[] line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Length <= MaxLineLength)
        {
            return line;
        }

        var codePage = Assets.Font.CodePage;
        var kept = 0;

        while (kept < MaxLineLength)
        {
            var width = codePage.IsLeadByte(line[kept]) && kept + 1 < line.Length ? 2 : 1;
            if (kept + width > MaxLineLength)
            {
                break;
            }

            kept += width;
        }

        return line[..kept];
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (Visible && key.Type == UiEventType.KeyDown)
        {
            switch (key.Key)
            {
                case UiKeys.Up:
                    _position = Math.Max(0, _position - 1);
                    Invalidate();
                    return true;

                case UiKeys.Down:
                    _position = Math.Clamp(_position + 1, 0, Math.Max(0, _lines.Count - VisibleRows));
                    Invalidate();
                    return true;

                default:
                    break;
            }
        }

        return base.OnKey(in key);
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (Visible && pointer.Type == UiEventType.Wheel && pointer.Wheel != 0)
        {
            _position = Math.Clamp(_position - pointer.Wheel, 0, Math.Max(0, _lines.Count - VisibleRows));
            Invalidate();
            return true;
        }

        return base.OnPointer(in pointer);
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        base.OnDrawContent(canvas);

        for (var row = 0; row < VisibleRows; row++)
        {
            var index = _position + row;
            if ((uint)index >= (uint)_lines.Count)
            {
                break;
            }

            _text.SetText(_lines[index]);
            _text.Draw(canvas, _list.X, _list.Y + (row * RowHeight));
        }
    }

    private void Answer(int action)
    {
        Visible = false;
    }
}
