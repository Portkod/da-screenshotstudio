using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Records;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class LegendListPane : RollPane
{
    public const string IconEntryName = "_nui_leg.spf";

    public const int IconFrameCount = 8;

    public const int RowHeight = 0x14;

    public const int CellWidth = 0x11C;

    public const int EntriesPerRow = 1;

    public const int TextIndent = 0x1C;

    public const int TextBaseline = 6;

    public const int Capacity = 255;

    private readonly Pixmap[] _icons = new Pixmap[IconFrameCount];
    private readonly byte[] _icon = new byte[Capacity];
    private readonly byte[] _color = new byte[Capacity];
    private readonly byte[] _text = new byte[Capacity * LegendMark.MaxTextLength];
    private readonly byte[] _textLength = new byte[Capacity];
    private readonly byte[] _key = new byte[Capacity * LegendMark.MaxKeyLength];
    private readonly byte[] _keyLength = new byte[Capacity];
    private readonly TextBlock _line;

    public LegendListPane(in PixelRect bounds, PixelBlender blender, UiAssets assets)
        : base(in bounds, blender, RowHeight, EntriesPerRow, CellWidth)
    {
        ArgumentNullException.ThrowIfNull(assets);

        for (var i = 0; i < _icons.Length; i++)
        {
            _icons[i] = assets.Frame(IconEntryName, i);
        }

        _line = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width - TextIndent), 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Left,
        };
    }

    public byte IconAt(int index) => _icon[CheckIndex(index)];

    public ReadOnlySpan<byte> KeyAt(int index) =>
        _key.AsSpan(CheckIndex(index) * LegendMark.MaxKeyLength, _keyLength[index]);

    public bool ContainsKey(scoped ReadOnlySpan<char> key)
    {
        for (var i = 0; i < Count; i++)
        {
            var stored = _key.AsSpan(i * LegendMark.MaxKeyLength, _keyLength[i]);
            if (stored.Length != key.Length)
            {
                continue;
            }

            var same = true;
            for (var j = 0; j < stored.Length; j++)
            {
                if (stored[j] != (byte)key[j])
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }

    public void SetMarks(ReadOnlySpan<LegendMark> marks)
    {
        Count = 0;
        Selected = -1;

        foreach (var mark in marks)
        {
            if (Count == Capacity)
            {
                break;
            }

            ReadOnlySpan<byte> text = mark.Text;
            if (text.Length > LegendMark.MaxTextLength)
            {
                text = text[..LegendMark.MaxTextLength];
            }

            ReadOnlySpan<byte> key = mark.Key;
            if (key.Length > LegendMark.MaxKeyLength)
            {
                key = key[..LegendMark.MaxKeyLength];
            }

            _icon[Count] = (byte)mark.Icon;
            _color[Count] = mark.Color;
            _textLength[Count] = (byte)text.Length;
            text.CopyTo(_text.AsSpan(Count * LegendMark.MaxTextLength));
            _keyLength[Count] = (byte)key.Length;
            key.CopyTo(_key.AsSpan(Count * LegendMark.MaxKeyLength));
            Count++;
        }

        Position = Math.Min(Position, Maximum);
        Invalidate();
    }

    protected override void OnBoundsChanged()
    {
        _line.MaxWidth = Math.Max(1, Bounds.Width - TextIndent);
        base.OnBoundsChanged();
    }

    protected override void DrawRow(Canvas canvas, int index, int top)
    {
        var icon = _icon[index];
        if (icon < IconFrameCount && !_icons[icon].IsEmpty)
        {
            canvas.BlitPixmap(0, top, in _icons[icon], BlendMode.Transparent);
        }

        var pen = ((top + top + RowHeight) / 2) + TextBaseline;
        var textTop = pen - _line.LineHeight;

        _line.DefaultPaletteIndex = _color[index];
        _line.SetText(_text.AsSpan(index * LegendMark.MaxTextLength, _textLength[index]));
        _line.DrawIn(canvas, new PixelRect(TextIndent, textTop, canvas.Width - TextIndent, RowHeight));
    }
}
