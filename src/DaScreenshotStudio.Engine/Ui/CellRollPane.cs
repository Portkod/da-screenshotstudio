using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public abstract class CellRollPane : RollPane
{
    public const string CellLayoutName = "_nui_ski.txt";

    public const string CellImageControl = "Noname";

    public const string IconControl = "TILE";

    public const string NameControl = "NAME";

    public const string LevelControl = "LEVEL";

    public const int RowHeight = 0x2D;

    public const int CellWidth = 0xE1;

    public const int EntriesPerRow = 1;

    public const int TextPaletteIndex = 0xFF;

    public const int RecordLength = 0x20C;

    public const int RecordTextLength = 0x100;

    private readonly TextBlock _name;
    private readonly TextBlock _level;
    private readonly IconFrame _cell;
    private readonly PixelRect _iconBox;
    private readonly PixelRect _nameBox;
    private readonly PixelRect _levelBox;

    private byte[] _text = [];
    private byte[] _nameLength = [];
    private byte[] _lineLength = [];

    protected CellRollPane(in PixelRect bounds, PixelBlender blender, UiAssets assets)
        : base(in bounds, blender, RowHeight, EntriesPerRow, CellWidth)
    {
        ArgumentNullException.ThrowIfNull(assets);

        var cell = assets.Layout(CellLayoutName);
        _cell = cell is not null && cell.TryFind(CellImageControl, out var image) && image.Images.Count > 0
            ? assets.PlacedFrame(image.Images[0].EntryName, image.Images[0].Frame)
            : default;

        GuiLayoutGeometry.TryRect(cell, IconControl, out _iconBox);
        GuiLayoutGeometry.TryRect(cell, NameControl, out _nameBox);
        GuiLayoutGeometry.TryRect(cell, LevelControl, out _levelBox);

        _name = NewLine(assets, bounds.Width - _nameBox.X);
        _level = NewLine(assets, bounds.Width - _levelBox.X);
    }

    public ReadOnlySpan<byte> NameAt(int index) =>
        _text.AsSpan(CheckIndex(index) * RecordTextLength * 2, _nameLength[index]);

    public ReadOnlySpan<byte> LineAt(int index) =>
        _text.AsSpan((CheckIndex(index) * RecordTextLength * 2) + RecordTextLength, _lineLength[index]);

    protected abstract IconFrame IconFor(int index);

    protected void GrowRows(int rows)
    {
        if (_nameLength.Length >= rows)
        {
            return;
        }

        _nameLength = new byte[rows];
        _lineLength = new byte[rows];
        _text = new byte[rows * RecordTextLength * 2];
    }

    protected Span<byte> RowText(int index) =>
        _text.AsSpan(index * RecordTextLength * 2, RecordTextLength * 2);

    protected void SetRowLengths(int index, int nameLength, int lineLength)
    {
        _nameLength[index] = (byte)Math.Min(nameLength, byte.MaxValue);
        _lineLength[index] = (byte)Math.Min(lineLength, byte.MaxValue);
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (pointer.Type == UiEventType.LeftDown && Contains(in pointer))
        {
            var row = RowAt(pointer.X, pointer.Y);
            if (row >= 0)
            {
                if (IsFocusable)
                {
                    FocusRequested?.Invoke(this);
                }

                return true;
            }
        }

        return base.OnPointer(in pointer);
    }

    protected override void OnBoundsChanged()
    {
        _name.MaxWidth = Math.Max(1, Bounds.Width - _nameBox.X);
        _level.MaxWidth = Math.Max(1, Bounds.Width - _levelBox.X);
        base.OnBoundsChanged();
    }

    protected override void DrawRow(Canvas canvas, int index, int top)
    {
        if (!_cell.IsEmpty)
        {
            canvas.BlitPixmap(_cell.Left, top + _cell.Top, in _cell.Image, BlendMode.Transparent);
        }

        DrawLine(canvas, _name, in _nameBox, top, NameAt(index));
        DrawLine(canvas, _level, in _levelBox, top, LineAt(index));

        var icon = IconFor(index);
        if (!icon.IsEmpty)
        {
            canvas.BlitPixmap(
                _iconBox.X + icon.Left,
                top + _iconBox.Y + icon.Top,
                in icon.Image,
                BlendMode.Transparent);
        }
    }

    private static TextBlock NewLine(UiAssets assets, int width) =>
        new(assets.Font, assets.Palette, Math.Max(1, width), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
            DefaultPaletteIndex = TextPaletteIndex,
        };

    private static void DrawLine(
        Canvas canvas,
        TextBlock block,
        in PixelRect box,
        int top,
        scoped ReadOnlySpan<byte> text)
    {
        block.SetText(text);
        block.DrawIn(
            canvas,
            new PixelRect(box.X, top + box.Bottom - block.LineHeight, canvas.Width - box.X, block.LineHeight));
    }
}
