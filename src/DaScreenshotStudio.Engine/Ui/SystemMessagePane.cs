using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class SystemMessagePane : Pane
{
    public const int RowHeight = 12;

    public const int MinimumLines = 1;

    public const int MaximumLines = 10;

    public const string ArtEntryName = "_nsmbk.spf";

    public const string WrapControlName = "SystemMessageWrap";

    public const int TextContentWidth = 0x1B7;

    public const string TextControlName = "SystemMessage";

    public const string ArtControlName = "SystemMessagePane";

    private readonly Pixmap _skin;
    private readonly LogViewPane _text;
    private readonly int _originX;
    private readonly int _originY;
    private readonly int _width;
    private readonly int _textOffsetX;
    private readonly int _textWidth;
    private int _lines = MinimumLines;
    private bool _dragging;

    public SystemMessagePane(
        in PixelRect wrap,
        in PixelRect textRect,
        in Pixmap skin,
        PixelBlender blender,
        GameFont font,
        TextPalette palette,
        TextLog log)
        : base(new PixelRect(wrap.X, wrap.Y, wrap.Width, wrap.Height), blender)
    {
        _skin = skin;
        _originX = wrap.X;
        _originY = wrap.Y;
        _width = wrap.Width;
        HeaderHeight = textRect.Y - wrap.Y;
        FooterHeight = wrap.Bottom - textRect.Bottom;
        _textOffsetX = textRect.X - wrap.X;
        _textWidth = textRect.Width;

        TargetBlend = BlendMode.Transparent;

        _text = new LogViewPane(
            new PixelRect(_textOffsetX, HeaderHeight, _textWidth, RowHeight),
            blender,
            font,
            palette,
            log,
            TextContentWidth)
        {
            Background = skin,
            DefaultPaletteIndex = TextMarkup.MessageHistory,
            ScrollsOnWheel = false,
        };

        Add(_text);
        SetVisibleLines(MinimumLines);
    }

    public int HeaderHeight { get; }

    public int FooterHeight { get; }

    public int VisibleLines => _lines;

    public bool IsDragging => _dragging;

    public LogViewPane Text => _text;

    public int HeightFor(int lines) => HeaderHeight + (RowHeight * Math.Clamp(lines, MinimumLines, MaximumLines)) + FooterHeight;

    public void SetVisibleLines(int lines)
    {
        _lines = Math.Clamp(lines, MinimumLines, MaximumLines);
        SetBounds(new PixelRect(_originX, _originY, _width, HeightFor(_lines)));
        _text.SetBounds(new PixelRect(_textOffsetX, HeaderHeight, _textWidth, RowHeight * _lines));
        Invalidate();
    }

    public void Refresh() => _text.Refresh();

    protected override bool OnPointer(in PointerEvent pointer)
    {
        switch (pointer.Type)
        {
            case UiEventType.LeftUp:
                if (_dragging)
                {
                    SetVisibleLines(MinimumLines);
                    _dragging = false;
                }

                return false;

            case UiEventType.LeftDown when Contains(in pointer):
                _dragging = true;
                return true;

            case UiEventType.PointerMove when _dragging:
                while (pointer.Y > Bounds.Height && _lines < MaximumLines)
                {
                    SetVisibleLines(_lines + 1);
                }

                while (pointer.Y < Bounds.Height - RowHeight && _lines > MinimumLines)
                {
                    SetVisibleLines(_lines - 1);
                }

                return true;

            default:
                return false;
        }
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (_skin.IsEmpty)
        {
            return;
        }

        var body = Math.Min(HeaderHeight + (RowHeight * _lines), _skin.Height);
        canvas.BlitPixmap(0, 0, _skin.Rows(0, body), BlendMode.Transparent);

        var footer = Math.Min(FooterHeight, _skin.Height);
        canvas.BlitPixmap(0, body, _skin.Rows(_skin.Height - footer, footer), BlendMode.Transparent);
    }
}
