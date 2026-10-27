using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed class MessagePopupPane : Pane
{
    public static PixelRect RecoveredRect => new(140, 60, 360, 180);

    public const int CellSize = 12;

    public const string AlternateArtName = "woodbk.epf";

    public const int CloseButtonFrame = 3;

    public const int CloseButtonInset = 0xA;

    private const byte TabByte = 0x09;

    private const byte CarriageReturn = 0x0D;

    public const byte TextPaletteIndex = 0x14;

    public const int MaxMessageBytes = short.MaxValue;

    public const int MaxRetainedBytes = 0x2710;

    private readonly UiAssets _assets;
    private readonly PixelBlender _blender;
    private readonly DialogSkin _skin;
    private readonly GuiTextPane? _text;
    private readonly ScrollBarPane? _bar;
    protected override ScrollBarPane? WheelScrollBar(int index) => _bar;

    protected override bool RoutesWheelToControls => true;

    protected override PixelRect WheelControlArea(int index) => _content;
    private readonly ImageButtonPane? _close;
    private readonly byte[] _body = new byte[MaxRetainedBytes];
    private readonly byte[] _laidOut = new byte[MaxRetainedBytes];
    private readonly PixelRect _content;
    private readonly Pixmap _alternateArt;
    private int _length;

    public MessagePopupPane(PixelBlender blender, UiAssets assets)
        : this(blender, assets, RecoveredRect)
    {
    }

    public MessagePopupPane(PixelBlender blender, UiAssets assets, in PixelRect bounds)
        : base(in bounds, blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
        _blender = blender;
        _skin = new DialogSkin(assets);

        TargetBlend = BlendMode.Copy;
        Visible = false;

        ConsumesPointerAnywhere = true;

        var cellsAcross = ((Bounds.Width - 0x28) / CellSize) * CellSize;
        var cellsDown = ((Bounds.Height - 0x32) / CellSize) * CellSize;
        _content = PixelRect.FromEdges(0x0C, 0x10, cellsAcross + 0x20, cellsDown + 0x10);

        _alternateArt = assets.LegendFrame(AlternateArtName, 0);

        (_text, _bar) = BuildContent();
        _close = BuildCloseButton();
        _standardTextWidth = _text?.Bounds.Width ?? 0;
    }

    public int TextWidth => _text?.Bounds.Width ?? 0;

    private readonly int _standardTextWidth;

    public bool IsAlternate { get; private set; }

    public byte MessageType { get; private set; }

    public ReadOnlySpan<byte> Body => _body.AsSpan(0, _length);

    public PixelRect ContentRect => _content;

    public ScrollBarPane? Bar => _bar;

    public int ScrollMaximum => _bar?.Maximum ?? 0;

    public int ScrollPosition => _bar?.Position ?? 0;

    public Action? Closed { get; set; }

    public bool Show(byte messageType, scoped ReadOnlySpan<byte> text)
    {
        if (messageType is not (0x08 or 0x09 or 0x0A))
        {
            return false;
        }

        MessageType = messageType;

        IsAlternate = messageType == 0x0A;
        ApplyPresentation();

        Fill(text, convertTabs: true);
        return true;
    }

    public void ShowText(scoped ReadOnlySpan<byte> text)
    {
        MessageType = 0;
        IsAlternate = false;
        ApplyPresentation();

        Fill(text, convertTabs: false);
    }

    public void Close()
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        MessageType = 0;
        _length = 0;
        _close?.ResetState();
        Closed?.Invoke();
        Invalidate();
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        if (key.Key is UiKeys.Escape or UiKeys.Enter or UiKeys.Space)
        {
            Close();
            return true;
        }

        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!Visible)
        {
            return;
        }

        if (IsAlternate)
        {
            DialogSkin.DrawNamedBackground(
                canvas, in _alternateArt, _assets.Palette[DialogSkin.NamedBackgroundFillIndex]);
            return;
        }

        _skin.Draw(canvas);
    }

    private void ApplyPresentation()
    {
        if (_bar is { } bar)
        {
            bar.Visible = !IsAlternate;
        }

        if (_close is { } close)
        {
            close.Visible = !IsAlternate;
        }

        if (_text is { } text)
        {
            var width = IsAlternate ? _content.Width : _standardTextWidth;
            if (text.Bounds.Width != width)
            {
                text.SetBounds(new PixelRect(_content.X, _content.Y, width, _content.Height));
            }
        }
    }

    private void Fill(scoped ReadOnlySpan<byte> text, bool convertTabs)
    {
        _length = Math.Min(text.Length, _body.Length);
        text[.._length].CopyTo(_body);

        if (convertTabs)
        {
            for (var i = 0; i < _length; i++)
            {
                if (_body[i] == TabByte)
                {
                    _body[i] = CarriageReturn;
                }
            }
        }

        for (var i = 0; i < _length; i++)
        {
            _laidOut[i] = _body[i] == (byte)'\r' ? TextBlock.LineBreak : _body[i];
        }

        if (_text is not null)
        {
            _text.Text.MaxLines = Math.Max(VisibleLines, _length);
            _text.Text.MaxLines = Math.Max(VisibleLines, _text.Text.CountLines(_laidOut.AsSpan(0, _length)));
            _text.SetText(_laidOut.AsSpan(0, _length));
            _text.TopPadding = 0;
        }

        if (_bar is not null)
        {
            _bar.Maximum = Math.Max(0, RowCount - VisibleLines);
            _bar.PageStep = Math.Max(1, VisibleLines - 1);
            _bar.Position = 0;
        }

        Visible = true;
        Invalidate();
    }

    private int VisibleLines => Math.Max(1, (_text?.Bounds.Height ?? 0) / _assets.Font.LineHeight);

    private int RowCount =>
        _text is null ? 0 : (_text.Text.Height + _assets.Font.LineHeight - 1) / _assets.Font.LineHeight;

    private (GuiTextPane? Text, ScrollBarPane? Bar) BuildContent()
    {
        var frames = ScrollBarPane.LoadFrames(_assets);
        var thickness = ScrollBarPane.ThicknessOf(frames);
        var width = Math.Max(1, _content.Width - thickness);

        var text = new GuiTextPane(
            new PixelRect(_content.X, _content.Y, width, _content.Height),
            _blender,
            _assets.Font,
            _assets.Palette,
            maxLines: Math.Max(1, _content.Height / _assets.Font.LineHeight))
        {
            Wraps = true,
            IsScrolled = true,
            PaletteIndex = TextPaletteIndex,
        };

        Add(text);

        if (frames is null)
        {
            return (text, null);
        }

        var bar = new ScrollBarPane(
            new PixelRect(_content.Right - thickness, _content.Y, thickness, _content.Height),
            _blender,
            frames,
            horizontal: false);

        bar.Scrolled = () =>
        {
            text.TopPadding = -bar.Position * _assets.Font.LineHeight;
            text.Invalidate();
        };

        Add(bar);
        return (text, bar);
    }

    private ImageButtonPane? BuildCloseButton()
    {
        var measure = _assets.PaletteFrame(LayoutPane.PresetButtonArt, 0);
        if (measure.IsEmpty)
        {
            return null;
        }

        var pane = new ImageButtonPane(
            new PixelRect(
                Bounds.Width - CloseButtonInset - measure.Width,
                Bounds.Height - CloseButtonInset - measure.Height,
                measure.Width,
                measure.Height),
            _blender)
        {
            DrawsFocusOutline = true,
            Palette = _assets.Palette,
            States = ImageButtonStates.DialogPress,
            Activated = _ => Close(),
        };

        pane.SetFrames(
            _assets.PaletteFrame(LayoutPane.PresetButtonArt, CloseButtonFrame),
            _assets.PaletteFrame(LayoutPane.PresetButtonArt, CloseButtonFrame + 1),
            _assets.PaletteFrame(LayoutPane.PresetButtonArt, CloseButtonFrame + 2));

        pane.FocusRequested = FocusControl;

        Add(pane);
        return pane;
    }
}

public sealed class ConfirmPane : Pane
{
    public const int MaxPromptBytes = 512;

    public const int MaxValueBytes = 256;

    private const int Padding = 8;
    private const int ButtonWidth = 64;
    private const int ButtonHeight = 18;

    private readonly UiAssets _assets;
    private readonly TextBlock _prompt;
    private readonly TextBlock _buttonText;
    private readonly byte[] _promptBytes = new byte[MaxPromptBytes];
    private readonly byte[] _value = new byte[MaxValueBytes];

    private int _promptLength;
    private int _valueLength;
    private int _hovered = -1;

    public ConfirmPane(PixelBlender blender, UiAssets assets)
        : base(new PixelRect(160, 190, 320, 100), blender)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
        TargetBlend = BlendMode.Transparent;
        Visible = false;

        ConsumesPointerAnywhere = true;

        _prompt = new TextBlock(assets.Font, assets.Palette, Bounds.Width - (Padding * 2), 4);
        _buttonText = new TextBlock(assets.Font, assets.Palette, ButtonWidth, 1)
        {
            Wraps = false,
            Alignment = TextAlignment.Center,
        };
    }

    public byte DialogValue0 { get; private set; }

    public byte DialogValue1 { get; private set; }

    public ReadOnlySpan<byte> Value => _value.AsSpan(0, _valueLength);

    public ReadOnlySpan<byte> Prompt => _promptBytes.AsSpan(0, _promptLength);

    public PixelRect OkRect =>
        new(Padding, Bounds.Height - ButtonHeight - Padding, ButtonWidth, ButtonHeight);

    public PixelRect CancelRect =>
        new(Bounds.Width - ButtonWidth - Padding, Bounds.Height - ButtonHeight - Padding, ButtonWidth, ButtonHeight);

    public void Show(byte dialogValue0, byte dialogValue1, scoped ReadOnlySpan<byte> value, scoped ReadOnlySpan<byte> prompt)
    {
        DialogValue0 = dialogValue0;
        DialogValue1 = dialogValue1;

        _valueLength = Math.Min(value.Length, MaxValueBytes);
        value[.._valueLength].CopyTo(_value);

        _promptLength = Math.Min(prompt.Length, MaxPromptBytes);
        prompt[.._promptLength].CopyTo(_promptBytes);
        _prompt.SetText(Prompt);

        Visible = true;
        Invalidate();
    }

    public void Answer(bool accepted)
    {
        if (!Visible)
        {
            return;
        }

        Visible = false;
        _hovered = -1;
        Invalidate();
    }

    protected override bool OnKey(in KeyEvent key)
    {
        if (!Visible || key.Type != UiEventType.KeyDown)
        {
            return false;
        }

        switch (key.Key)
        {
            case UiKeys.Enter or UiKeys.Space:
                Answer(true);
                return true;

            case UiKeys.Escape:
                Answer(false);
                return true;

            default:
                return false;
        }
    }

    protected override bool OnPointer(in PointerEvent pointer)
    {
        if (!Visible)
        {
            return false;
        }

        var ok = OkRect.Contains(pointer.X, pointer.Y);
        var cancel = CancelRect.Contains(pointer.X, pointer.Y);
        var hovered = ok ? 0 : cancel ? 1 : -1;

        if (pointer.Type == UiEventType.PointerMove)
        {
            if (_hovered != hovered)
            {
                _hovered = hovered;
                Invalidate();
            }

            return false;
        }

        if (pointer.Type == UiEventType.LeftUp && hovered >= 0)
        {
            Answer(ok);
        }

        return false;
    }

    protected override void OnDrawContent(Canvas canvas)
    {
        canvas.Clear();

        if (!Visible)
        {
            return;
        }

        var box = new PixelRect(0, 0, Bounds.Width, Bounds.Height);
        canvas.FillRect(in box, _assets.Palette[0x1C], BlendMode.Copy);
        canvas.SetDrawColor(_assets.Palette[0x18]);
        canvas.DrawRectOutline(in box);

        _prompt.DrawIn(canvas, new PixelRect(Padding, Padding, box.Width - (Padding * 2), box.Height - 40));

        DrawButton(canvas, OkRect, "OK"u8, _hovered == 0);
        DrawButton(canvas, CancelRect, "Cancel"u8, _hovered == 1);
    }

    private void DrawButton(Canvas canvas, in PixelRect rect, scoped ReadOnlySpan<byte> label, bool hovered)
    {
        canvas.FillRect(in rect, _assets.Palette[hovered ? 0x1A : 0x1C], BlendMode.Copy);
        canvas.SetDrawColor(_assets.Palette[0x18]);
        canvas.DrawRectOutline(in rect);

        _buttonText.SetText(label);
        _buttonText.DrawIn(canvas, in rect);
    }
}
