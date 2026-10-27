using DaScreenshotStudio.Engine.App;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class GameMessagePane : Pane
{
    public const int RowCapacity = 4;

    public const int VisibleRowCapacity = RowCapacity - 1;

    public const int RowHeight = 14;

    public const int CellWidth = 6;

    public const int CellsPerRow = 49;

    public const int MaxCellsPerLine = CellsPerRow - 1;

    public const int GlyphCellHeight = 12;

    public const int GlyphCellInset = RowHeight - GlyphCellHeight;

    public const int LeadingCells = 1;

    public const int ShadowOffset = 1;

    public const byte ShadowIndex = TextPalette.ShadowIndex;

    public const int HoldMs = 2000;

    public const int FadeIntervalMs = 50;

    public const int FadeSteps = 32;

    private readonly TextLog _rows = new(VisibleRowCapacity, VisibleRowCapacity * CellsPerRow);
    private readonly TextBlock[] _ink = new TextBlock[VisibleRowCapacity];
    private readonly TextBlock[] _shadow = new TextBlock[VisibleRowCapacity];
    private byte[] _pending = new byte[CellsPerRow];
    private ushort[] _pendingColors = new ushort[CellsPerRow];
    private int _pendingLength;
    private uint _fadeDue;
    private int _remaining;
    private int _weight = PixelBlender.MaxAlphaWeight;

    public GameMessagePane(in PixelRect bounds, PixelBlender blender, GameFont font, TextPalette palette)
        : base(in bounds, blender)
    {
        TargetBlend = BlendMode.Transparent;
        Visible = false;

        var width = Math.Max(1, CellsPerRow * CellWidth);

        for (var row = 0; row < VisibleRowCapacity; row++)
        {
            _ink[row] = new TextBlock(font, palette, width, 1) { Wraps = false };
            _shadow[row] = new TextBlock(font, palette, width, 1)
            {
                Wraps = false,
                DefaultPaletteIndex = ShadowIndex,
            };
        }
    }

    public static PixelRect ScreenBounds => PixelRect.FromEdges(
        3,
        TopMargin,
        0x12C,
        TopMargin + (RowCapacity * RowHeight));

    public const int TopMargin = 2;

    public TextLog Rows => _rows;

    public int Weight => _weight;

    public void Append(scoped ReadOnlySpan<byte> text, byte paletteIndex, uint now)
    {
        AppendRows(text, paletteIndex, default);
        Show(now);
    }

    public void AppendRgb(scoped ReadOnlySpan<byte> text, ushort color, uint now)
    {
        while (!text.IsEmpty)
        {
            var breakAt = text.IndexOf(TextBlock.LineBreak);
            var run = breakAt < 0 ? text : text[..breakAt];

            if (!run.IsEmpty)
            {
                if (_pendingLength + run.Length > _pending.Length)
                {
                    Array.Resize(ref _pending, Math.Max(_pendingLength + run.Length, _pending.Length * 2));
                    Array.Resize(ref _pendingColors, _pending.Length);
                }

                run.CopyTo(_pending.AsSpan(_pendingLength));
                _pendingColors.AsSpan(_pendingLength, run.Length).Fill(color);
                _pendingLength += run.Length;
            }

            if (breakAt < 0)
            {
                return;
            }

            Commit(now);
            text = text[(breakAt + 1)..];
        }
    }

    private void Commit(uint now)
    {
        AppendRows(
            _pending.AsSpan(0, _pendingLength),
            TextPalette.WhiteIndex,
            _pendingColors.AsSpan(0, _pendingLength));

        _pendingLength = 0;
        Show(now);
    }

    private void AppendRows(scoped ReadOnlySpan<byte> text, byte paletteIndex, scoped ReadOnlySpan<ushort> colors)
    {
        var take = Math.Min(MaxCellsPerLine, text.Length);
        _rows.Append(text[..take], paletteIndex, colors.IsEmpty ? default : colors[..take]);
    }

    private void Show(uint now)
    {
        _remaining = FadeSteps;
        _weight = PixelBlender.MaxAlphaWeight;
        _fadeDue = unchecked(now + HoldMs);

        TargetBlend = BlendMode.Transparent;
        TargetAlphaWeight = PixelBlender.MaxAlphaWeight;
        Visible = true;
        Invalidate();
    }

    public bool Tick(uint now)
    {
        if (!Visible || !GameClock.IsDue(now, _fadeDue))
        {
            return false;
        }

        if (_remaining == 0)
        {
            Visible = false;
            return true;
        }

        _weight = Math.Min(_remaining - 1, PixelBlender.MaxAlphaWeight);
        _remaining--;
        TargetBlend = BlendMode.Alpha;
        TargetAlphaWeight = _weight;
        _fadeDue = unchecked(now + FadeIntervalMs);
        return true;
    }

    public void Clear()
    {
        if (!Visible && _rows.Count == 0)
        {
            return;
        }

        _rows.Clear();
        _pendingLength = 0;
        Visible = false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        var count = Math.Min(_rows.Count, VisibleRowCapacity);

        for (var row = 0; row < count; row++)
        {
            var text = _rows[row];
            if (text.IsEmpty)
            {
                continue;
            }

            var colors = _rows.ColorsAt(row);
            var ink = _ink[row];
            var shadow = _shadow[row];

            ink.DefaultPaletteIndex = _rows.PaletteAt(row);
            ink.SetText(text);
            ink.SetExplicitColors(colors);
            shadow.SetText(text);

            var left = LeadingCells * CellWidth;
            var top = (row * RowHeight) + GlyphCellInset;

            shadow.Draw(canvas, left + ShadowOffset, top + ShadowOffset);
            ink.Draw(canvas, left, top);
        }
    }
}
