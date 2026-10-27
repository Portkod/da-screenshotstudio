using DaScreenshotStudio.Assets.Tables;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class KeyboardInfoPane : Pane
{
    public const string LayoutName = "lkey.txt";

    public const int KeyCount = 12;

    public const int NoKey = -1;

    public const int CaptionSlot = 0x5A;

    public const byte CaptionInk = 0xFF;

    public const int CaptionLineHeight = 0xC;

    public const int CaptionCellWidth = 6;

    public const string BackgroundControl = "Noname";

    public const string ContentControl = "CONTENT";

    public const string HotkeyControl = "HOTKEY";

    public const string DescriptionControl = "DESC";

    private readonly Pixmap _background;
    private readonly Pixmap _hotkey;
    private readonly Pixmap[] _keys = new Pixmap[KeyCount];
    private readonly Pixmap[] _descriptions = new Pixmap[KeyCount];
    private readonly PixelRect[] _keyRects = new PixelRect[KeyCount];
    private readonly PixelRect _contentRect;
    private readonly PixelRect _hotkeyRect;
    private readonly TextBlock _caption;
    private readonly MessageTable _messages;

    private int _selected = NoKey;

    public KeyboardInfoPane(UiAssets assets, PixelBlender blender, in PixelRect bounds)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _messages = assets.Messages;
        _caption = new TextBlock(assets.Font, assets.Palette, Math.Max(1, bounds.Width), 1)
        {
            DefaultPaletteIndex = CaptionInk,
        };

        var layout = assets.Layout(LayoutName);
        _background = GuiLayoutGeometry.ControlFrame(assets, layout, BackgroundControl, 0);
        _hotkey = GuiLayoutGeometry.ControlFrame(assets, layout, HotkeyControl, 0);

        GuiLayoutGeometry.TryRect(layout, ContentControl, out _contentRect);

        _hotkeyRect = GuiLayoutGeometry.TryRect(layout, HotkeyControl, out var hotkey)
            ? Shifted(in hotkey, in _contentRect)
            : PixelRect.Empty;

        for (var i = 0; i < KeyCount; i++)
        {
            var name = $"KEY{i:D2}";
            _keyRects[i] = GuiLayoutGeometry.TryRect(layout, name, out var rect)
                ? Shifted(in rect, in _contentRect)
                : PixelRect.Empty;

            _keys[i] = GuiLayoutGeometry.ControlFrame(assets, layout, name, 0);

            _descriptions[i] = GuiLayoutGeometry.ControlFrame(assets, layout, DescriptionControl, i);
        }
    }

    public ReadOnlySpan<byte> Caption => _messages.Get(CaptionSlot);

    public PixelRect CaptionRect()
    {
        var text = Caption;
        var width = text.Length * CaptionCellWidth;
        var penX = ((_contentRect.Width - width) / 2) + _contentRect.X;
        var penY = ((_hotkeyRect.Y - _contentRect.Y - CaptionLineHeight) / 2) + _contentRect.Y;
        return new PixelRect(penX, penY - CaptionLineHeight, Math.Max(0, width), CaptionLineHeight);
    }

    public int KeyAt(int x, int y)
    {
        for (var i = 0; i < KeyCount; i++)
        {
            var rect = _keyRects[i];
            if (rect.Width <= 0 || x < rect.X || x >= rect.Right || y < rect.Y || y >= rect.Bottom)
            {
                continue;
            }

            var art = _keys[i];
            var dx = x - rect.X;
            var dy = y - rect.Y;
            if (art.IsEmpty || dy >= art.Height || dx >= art.Width)
            {
                continue;
            }

            if (art.Row(dy)[dx] != 0)
            {
                return i;
            }
        }

        return NoKey;
    }

    public void SelectKey(int index)
    {
        var value = (uint)index < KeyCount ? index : NoKey;
        if (value == _selected)
        {
            return;
        }

        _selected = value;
        Invalidate();
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Contains(in pointer))
        {
            return false;
        }

        if (pointer.Type != UiEventType.LeftDown)
        {
            return true;
        }

        SelectKey(KeyAt(pointer.X, pointer.Y));
        return true;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        canvas.Clear();

        if (!_background.IsEmpty)
        {
            canvas.BlitPixmap(0, 0, in _background, BlendMode.Transparent);
        }

        if (!_hotkey.IsEmpty)
        {
            canvas.BlitPixmap(_hotkeyRect.X, _hotkeyRect.Y, in _hotkey, BlendMode.Transparent);
        }

        if ((uint)_selected < KeyCount)
        {
            DrawSelectedKey(canvas);
            return;
        }

        DrawCaption(canvas);
    }

    private void DrawSelectedKey(Canvas canvas)
    {
        var description = _descriptions[_selected];
        if (!description.IsEmpty)
        {
            canvas.BlitPixmap(_contentRect.X, _contentRect.Y, in description, BlendMode.Transparent);
        }

        var art = _keys[_selected];
        if (!art.IsEmpty)
        {
            var rect = _keyRects[_selected];
            canvas.BlitPixmap(rect.X, rect.Y, in art, BlendMode.Transparent);
        }
    }

    private void DrawCaption(Canvas canvas)
    {
        var text = Caption;
        if (text.IsEmpty)
        {
            return;
        }

        var rect = CaptionRect();
        _caption.SetText(text);
        _caption.Draw(canvas, rect.X, rect.Y);
    }

    private static PixelRect Shifted(in PixelRect rect, in PixelRect origin) =>
        new(rect.X + origin.X, rect.Y + origin.Y, rect.Width, rect.Height);
}
